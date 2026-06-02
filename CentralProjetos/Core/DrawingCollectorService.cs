using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using DrawingCollector.Core.Models;
using DrawingCollector.Logging;

namespace DrawingCollector.Core
{
    /// <summary>
    /// Orquestra o processo completo: indexação, matching, cópia.
    /// </summary>
    public class DrawingCollectorService
    {
        private readonly ILogger     _logger;
        private readonly FileIndexer _indexer;

        public DrawingCollectorService(ILogger logger)
        {
            _logger  = logger ?? throw new ArgumentNullException(nameof(logger));
            _indexer = new FileIndexer(logger);
        }

        public Action<MatchFoundEvent>? OnMatchFound { get; set; }
        public Action<int>?            OnProgress   { get; set; }

        /// <summary>
        /// Divergências de revisão detectadas na última execução.
        /// Preenchido durante Run() quando há RevisionRepository.
        /// </summary>
        public List<Reports.RevisionDivergence> RevisionDivergences { get; } = new();

        /// <summary>
        /// Executa a busca/cópia conforme as opções.
        /// Retorna o dicionário de códigos faltantes por extensão.
        /// </summary>
        public Dictionary<string, List<string>> Run(
            CollectorOptions options,
            CancellationToken token)
        {
            string[] uniqueCodes = options.Codes.Distinct().ToArray();
            int      total       = uniqueCodes.Length;
            var      extSet      = new HashSet<string>(options.Extensions,
                                       StringComparer.OrdinalIgnoreCase);

            var missingByExt = options.Extensions.ToDictionary(
                e => e,
                _ => new List<string>(),
                StringComparer.OrdinalIgnoreCase);

            // ── Indexação ────────────────────────────────────────────────
            _logger.Info($"Indexando {options.ServerRoots.Length} raiz(es) ...");
            FileIndex index = _indexer.BuildIndex(options.ServerRoots, extSet);
            OnProgress?.Invoke(50);

            // ── Logs de acompanhamento ───────────────────────────────────
            string copiedLog    = Path.Combine(options.OutputDirectory, "copied.log");
            string ambiguousLog = Path.Combine(options.OutputDirectory, "ambiguous.log");
            TryDelete(copiedLog);
            TryDelete(ambiguousLog);
            int copiedCount = 0;

            // ── Loop por código ──────────────────────────────────────────
            for (int i = 0; i < total; i++)
            {
                token.ThrowIfCancellationRequested();

                string code = uniqueCodes[i];
                OnProgress?.Invoke(50 + (int)((i + 1) * 50.0 / Math.Max(1, total)));

                string safeCode   = SanitizeFileName(code);
                string destFolder = options.GroupByCode
                    ? Path.Combine(options.OutputDirectory, safeCode)
                    : options.OutputDirectory;

                if (!Directory.Exists(destFolder))
                    Directory.CreateDirectory(destFolder);

                // ── Modo Barramento: consulta o banco antes de buscar ────────
                // Constrói um set de extensões "dispensadas" para este código específico.
                // Fora do modo barramento o set fica vazio (sem impacto).
                //
                // Exemplo:
                //   Modo barramento ON + código ABC123X0 + TemDobra = Não
                //   → dispensedByBusbar = { ".stp" }
                //   → quando for registrar faltantes, .stp é pulado para este código
                var dispensedByBusbar = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (options.BusbarMode && options.BusbarRepository != null)
                {
                    if (options.BusbarRepository.TryGetEntry(code, out var entry) && entry != null)
                    {
                        if (!entry.TemDobra)
                        {
                            // Sem dobra: .stp é dispensado para este código
                            dispensedByBusbar.Add(".stp");
                            _logger.Info($"[Barramento] {code}: sem dobra → .stp dispensado.");
                        }
                        else
                        {
                            _logger.Info($"[Barramento] {code}: com dobra → .dxf + .stp exigidos.");
                        }
                    }
                    else
                    {
                        // Código não está no banco → avisa mas não bloqueia
                        _logger.Warn($"[Barramento] {code}: não encontrado no banco. " +
                                     "Processando normalmente.");
                    }
                }

                // Busca matches para cada extensão pedida — O(1) por extensão
                var matches   = new List<string>();
                var foundExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (string wantedExt in options.Extensions)
                {
                    string ext = wantedExt.Trim().ToLowerInvariant();
                    if (string.IsNullOrEmpty(ext)) continue;

                    var mm = FindMatchesForExt(index, code, ext, options.ExactMode);
                    if (mm.Count > 0)
                    {
                        foundExts.Add(ext);

                        // Para códigos que começam com DÍGITO e acharam múltiplos
                        // arquivos (várias revisões), pega só a maior revisão.
                        // Códigos que começam com letra são exatos — não filtra.
                        string codeUp = code.Trim().ToUpperInvariant();
                        bool   isNumericBase = codeUp.Length > 0 && char.IsDigit(codeUp[0])
                                               && !CodeMatcher.IsSchneiderCode(codeUp);

                        if (isNumericBase && mm.Count > 1)
                        {
                            // Determina a base (remove revisão se o código já tiver)
                            string baseCode = codeUp;
                            var parsed = CodeMatcher.ExtractBaseAndRevision(codeUp);
                            if (parsed.HasValue) baseCode = parsed.Value.Base;

                            var best = CodeMatcher.SelectHighestRevision(mm, baseCode);
                            if (best != null) matches.Add(best);
                        }
                        else
                        {
                            matches.AddRange(mm);
                        }
                    }
                }

                // Ordena matches pela proximidade do código normalizado
                bool   isLv         = CodeMatcher.IsLvCode(code);
                string normalizedX0 = isLv ? "" : CodeMatcher.NormalizeSchneiderToX0(code);
                matches.Sort(new CodeMatcher.MatchComparer(options.ExactMode ? null : normalizedX0));

                string matchType = options.ExactMode
                    ? "EXATO (global)"
                    : "Por extensão (.pdf/.dwg=X0 | .stp/.dxf=EXATO)";

                // Reporta cada match para a UI via callback
                for (int k = 0; k < matches.Count; k++)
                {
                    string m   = matches[k];
                    string ext = Path.GetExtension(m).ToLowerInvariant();
                    OnMatchFound?.Invoke(new MatchFoundEvent(
                        IsFirstSelected: k == 0,
                        Code:            code,
                        FileName:        Path.GetFileName(m),
                        Extension:       ext,
                        MatchType:       matchType,
                        FullPath:        m));
                }

                // ── Registra faltantes ───────────────────────────────────────
                // Três camadas de dispensa (cada uma independente):
                //   1. foundExts:          extensão foi encontrada → não é faltante
                //   2. dispensedByBusbar:  modo barramento dispensou esta ext para este código
                //   3. ExtensionEquivalences: regra global (ex: .dwg dispensa .pdf)
                foreach (string wantedExt in options.Extensions)
                {
                    string want = wantedExt.Trim().ToLowerInvariant();
                    if (string.IsNullOrEmpty(want))            continue;
                    if (foundExts.Contains(want))              continue; // camada 1
                    if (dispensedByBusbar.Contains(want))      continue; // camada 2

                    // Camada 3: equivalências globais
                    bool dispensedByRule = false;
                    foreach (var rule in options.ExtensionEquivalences)
                    {
                        if (foundExts.Contains(rule.FoundExt.ToLowerInvariant()) &&
                            string.Equals(rule.DispensingExt.ToLowerInvariant(), want,
                                          StringComparison.OrdinalIgnoreCase))
                        {
                            dispensedByRule = true;
                            break;
                        }
                    }
                    if (!dispensedByRule)
                        missingByExt[want].Add(code);
                }

                if (options.PreviewOnly) continue;

                // ── Cópia: um arquivo por extensão ───────────────────────
                // ANTES: copiava só matches[0] — misturava extensões e perdia arquivos.
                // AGORA: para cada extensão pedida, copia o melhor match daquela extensão.
                //
                // Exemplo com .dxf e .stp:
                //   matches = ["LV-1716_2.6.dxf", "LV-1716_2.6.stp"]
                //   → copia LV-1716_2.6.dxf  (melhor match de .dxf)
                //   → copia LV-1716_2.6.stp  (melhor match de .stp)

                if (matches.Count == 0)
                {
                    _logger.Warn("Não encontrado: " + code);
                    continue;
                }

                // Agrupa matches por extensão e pega o primeiro de cada grupo
                // (já estão ordenados por qualidade graças ao MatchComparer acima)
                var bestByExt = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string m in matches)
                {
                    string mExt = Path.GetExtension(m).ToLowerInvariant();
                    if (!bestByExt.ContainsKey(mExt))
                        bestByExt[mExt] = m; // primeiro = melhor para esta extensão
                }

                // Verifica ambiguidade por extensão e registra no log
                var countByExt = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (string m in matches)
                {
                    string mExt = Path.GetExtension(m).ToLowerInvariant();
                    countByExt[mExt] = countByExt.GetValueOrDefault(mExt) + 1;
                }
                foreach (var kv in countByExt.Where(k => k.Value > 1))
                {
                    File.AppendAllText(ambiguousLog,
                        $"{code} [{kv.Key}] -> {kv.Value} arquivos " +
                        $"(copiado: {Path.GetFileName(bestByExt[kv.Key])})" +
                        Environment.NewLine);
                    _logger.Warn($"Vários {kv.Key} encontrados ({kv.Value}): {code}");
                }

                // Copia o melhor de cada extensão
                foreach (var kv in bestByExt)
                {
                    string best = kv.Value;
                    try
                    {
                        string dest   = Path.Combine(destFolder, Path.GetFileName(best));
                        bool   doCopy = !File.Exists(dest)
                                     || new FileInfo(best).LastWriteTimeUtc
                                      > new FileInfo(dest).LastWriteTimeUtc;

                        if (doCopy)
                        {
                            File.Copy(best, dest, true);
                            copiedCount++;
                            _logger.Ok("Copiado: " + Path.GetFileName(best));
                            File.AppendAllText(copiedLog,
                                code + " -> " + Path.GetFileName(best) + Environment.NewLine);
                        }
                        else
                        {
                            _logger.Info("Já atualizado: " + Path.GetFileName(best));
                        }
                    }
                    catch (IOException ex)
                    {
                        _logger.Err($"Falha ao copiar {best}: {ex.Message}");
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        _logger.Err($"Sem permissão para copiar {best}: {ex.Message}");
                    }
                }
            }

            if (!options.PreviewOnly && copiedCount == 0)
                _logger.Warn("Nenhum arquivo copiado.");

            // ── Validação de revisão ─────────────────────────────────────
            // Compara a revisão pedida na lista com a revisão oficial do banco.
            ValidateRevisions(options);

            OnProgress?.Invoke(100);
            return missingByExt;
        }

        /// <summary>
        /// Compara as revisões da lista de entrada com o banco de revisões oficiais.
        /// Preenche RevisionDivergences com os códigos divergentes.
        /// </summary>
        private void ValidateRevisions(CollectorOptions options)
        {
            RevisionDivergences.Clear();

            // Só valida se há banco de revisões carregado e códigos com revisão
            if (options.RevisionRepository == null || !options.RevisionRepository.IsLoaded)
                return;
            if (options.CodesWithRevision.Count == 0)
                return;

            int checados = 0;
            foreach (var item in options.CodesWithRevision)
            {
                // Só compara se a lista informou revisão para este código
                if (!item.HasRevision) continue;

                checados++;

                if (options.RevisionRepository.TryGetOfficialRevision(item.Code, out string oficial))
                {
                    // Normaliza para comparação:
                    //   "04" → "4", "0B" → "B", "0J" → "J", "01" → "1"
                    //   Remove zeros à esquerda e ignora maiúsculas/minúsculas
                    string revList    = NormalizeRevision(item.Revision);
                    string revOficial = NormalizeRevision(oficial);

                    bool igual = string.Equals(revList, revOficial,
                                               StringComparison.OrdinalIgnoreCase);

                    if (!igual)
                    {
                        RevisionDivergences.Add(new Reports.RevisionDivergence(
                            Code:             item.Code,
                            ListRevision:     item.Revision,
                            OfficialRevision: oficial,
                            Situation:        "Revisão divergente"));
                        _logger.Warn($"[Revisão] {item.Code}: lista pede '{item.Revision}', " +
                                     $"oficial é '{oficial}'.");
                    }
                }
                else
                {
                    // Código não está no banco de revisões
                    RevisionDivergences.Add(new Reports.RevisionDivergence(
                        Code:             item.Code,
                        ListRevision:     item.Revision,
                        OfficialRevision: "(não cadastrado)",
                        Situation:        "Código ausente no banco de revisões"));
                }
            }

            if (checados > 0)
            {
                if (RevisionDivergences.Count == 0)
                    _logger.Ok($"Validação de revisão: {checados} código(s) conferido(s), tudo OK.");
                else
                    _logger.Warn($"Validação de revisão: {RevisionDivergences.Count} " +
                                 $"divergência(s) em {checados} código(s) conferido(s).");
            }
        }

        /// <summary>
        /// Normaliza uma string de revisão para comparação.
        /// Remove zeros à esquerda para que "04" == "4", "0B" == "B", "0J" == "J".
        ///
        /// Exemplos:
        ///   "04"  → "4"
        ///   "0B"  → "B"
        ///   "0J"  → "J"
        ///   "B"   → "B"
        ///   "4"   → "4"
        ///   "01"  → "1"
        ///   "0"   → "0"   (não remove se for só "0")
        ///   "00"  → "0"
        ///   "0A"  → "A"
        /// </summary>
        private static string NormalizeRevision(string? rev)
        {
            if (string.IsNullOrWhiteSpace(rev)) return string.Empty;
            string trimmed = rev.Trim().ToUpperInvariant();

            // Remove zeros à esquerda, mas mantém pelo menos 1 caractere
            string normalized = trimmed.TrimStart('0');
            if (normalized.Length == 0) return "0"; // era "00" ou "0" → vira "0"

            return normalized;
        }

        // ════════════════════════════════════════════════════════════════
        //   BUSCA — O(1) com o novo FileIndex
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Retorna os arquivos que correspondem ao código para uma extensão específica.
        ///
        /// ANTES: percorria todos os arquivos do índice para cada código. O(N).
        /// AGORA: lookup direto nos dicionários pré-construídos.           O(1).
        ///
        /// Como funciona para cada estratégia:
        ///
        ///   ExactMode global → FileIndex.FindExact("51132814XD", ".dwg")
        ///     Chave exata no _exactIndex → resultado imediato.
        ///
        ///   SchneiderX0Only  → FileIndex.FindByX0("51132814XD", ".dwg")
        ///     Normaliza para X0 → "51132814X0"
        ///     Lookup no _x0Index → retorna TODOS do grupo (X0, XA, XB, XD...)
        ///     Um acesso, resultado completo.
        ///
        ///   ExactOnly        → FileIndex.FindExact(...)
        ///     Chave exata. Se não achar, retorna vazio (sem fallback X0).
        ///
        ///   ExactThenSchneider → tenta exato; se vazio, tenta X0.
        ///     Dois lookups no pior caso, mas ainda O(1).
        /// </summary>
        private static IReadOnlyList<string> FindMatchesForExt(
            FileIndex index,
            string    code,
            string    wantedExtLower,
            bool      exactModeFlag)
        {
            if (string.IsNullOrWhiteSpace(code))
                return Array.Empty<string>();

            string codeUp = code.Trim().ToUpperInvariant();

            // Modo EXATO global ativado pelo usuário → ignora todas as regras
            if (exactModeFlag)
                return index.FindByPrefix(codeUp, wantedExtLower);

            // ── Regra 1: Schneider (tem X+letra) → grupo X0 ────────────────
            // Ex: "51132814XD" → aceita qualquer revisão XA, XB, XD do grupo
            if (CodeMatcher.IsSchneiderCode(codeUp))
                return index.FindByX0(codeUp, wantedExtLower);

            // ── Regra 2: começa com LETRA → código EXATO ───────────────────
            // Ex: "ABM400014-05", "ABEL085002-012", "LV-0427", "P-SM-C-00759"
            // O traço-número faz parte do nome, NÃO é revisão.
            // Aceita o código exato + sufixo descritivo após espaço.
            if (char.IsLetter(codeUp[0]))
                return index.FindByPrefix(codeUp, wantedExtLower);

            // ── Regra 3: começa com DÍGITO → BASE + REVISÃO ────────────────
            // Ex: "3729054" → acha "3729054-J"
            //     "3736808-01" → base "3736808", acha maior revisão
            //     "51238828F002-02" → base "51238828F002", acha maior revisão
            // Determina a base: se já tem "-revisao", remove; senão usa o código todo.
            string baseCode = codeUp;
            var parsed = CodeMatcher.ExtractBaseAndRevision(codeUp);
            if (parsed.HasValue)
                baseCode = parsed.Value.Base;

            // Busca todos os arquivos cujo nome começa com a base + traço + revisão
            return index.FindByBaseRevision(baseCode, wantedExtLower);
        }

        // ════════════════════════════════════════════════════════════════
        //   UTILITÁRIOS
        // ════════════════════════════════════════════════════════════════

        public static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private static void TryDelete(string path)
        {
            try   { if (File.Exists(path)) File.Delete(path); }
            catch (IOException)                 { /* arquivo em uso */ }
            catch (UnauthorizedAccessException) { /* sem permissão  */ }
        }
    }

    public record MatchFoundEvent(
        bool   IsFirstSelected,
        string Code,
        string FileName,
        string Extension,
        string MatchType,
        string FullPath);
}

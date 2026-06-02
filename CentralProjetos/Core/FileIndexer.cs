using System;
using System.Collections.Generic;
using System.IO;
using DrawingCollector.Core.Models;
using DrawingCollector.Logging;

namespace DrawingCollector.Core
{
    /// <summary>
    /// Varre pastas e constrói um FileIndex com dois níveis de lookup.
    /// </summary>
    /// <remarks>
    /// O processo de indexação tem duas fases:
    ///
    /// FASE 1 — VARREDURA (I/O bound)
    ///   Percorre todas as pastas recursivamente e coleta os caminhos dos arquivos.
    ///   Esta é a parte lenta, pois depende da velocidade da rede/disco.
    ///
    /// FASE 2 — CONSTRUÇÃO DO ÍNDICE (CPU bound, muito rápida)
    ///   Para cada arquivo encontrado:
    ///     a) Quebra o nome-base em tokens (ex: "ABC123XD-03" → ["ABC123XD", "03"])
    ///     b) Para cada token, calcula o X0Key (normalização Schneider)
    ///     c) Adiciona ao _exactIndex (chave = token exato)
    ///        E ao _x0Index    (chave = token normalizado)
    ///
    /// O resultado é um FileIndex que responde a qualquer busca em O(1).
    /// </remarks>
    public class FileIndexer
    {
        private readonly ILogger _logger;

        public FileIndexer(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Constrói o índice global varrendo todas as raízes informadas.
        /// </summary>
        public FileIndex BuildIndex(string[] roots, HashSet<string> extSet)
        {
            var exactIndex = new Dictionary<string, List<IndexedFile>>(
                                 StringComparer.OrdinalIgnoreCase);
            var x0Index    = new Dictionary<string, List<IndexedFile>>(
                                 StringComparer.OrdinalIgnoreCase);
            var allFiles   = new List<IndexedFile>();

            int totalFiles = 0;

            foreach (string root in roots)
            {
                string trimmed = (root ?? "").Trim();
                if (!Directory.Exists(trimmed))
                {
                    _logger.Warn("Raiz inexistente/sem acesso: " + trimmed);
                    continue;
                }

                _logger.Info("• Varrendo: " + trimmed);
                int before = totalFiles;
                ScanDirectory(trimmed, extSet, exactIndex, x0Index, allFiles, ref totalFiles);
                _logger.Info($"  → {totalFiles - before} arquivos indexados.");
            }

            _logger.Ok($"Indexação concluída: {totalFiles} arquivos • " +
                       $"{exactIndex.Count} chaves exatas • " +
                       $"{x0Index.Count} grupos X0.");

            return new FileIndex(exactIndex, x0Index, allFiles);
        }

        private void ScanDirectory(
            string rootDir,
            HashSet<string> extSet,
            Dictionary<string, List<IndexedFile>> exactIndex,
            Dictionary<string, List<IndexedFile>> x0Index,
            List<IndexedFile> allFiles,
            ref int totalFiles)
        {
            var dirs = new Stack<string>();
            dirs.Push(rootDir);

            while (dirs.Count > 0)
            {
                string dir = dirs.Pop();
                try
                {
                    foreach (string sub in Directory.GetDirectories(dir))
                        dirs.Push(sub);

                    foreach (string filePath in Directory.GetFiles(dir))
                    {
                        string ext = Path.GetExtension(filePath).ToLowerInvariant();
                        if (extSet.Count > 0 && !extSet.Contains(ext)) continue;

                        IndexFile(filePath, ext, exactIndex, x0Index, allFiles);
                        totalFiles++;
                    }
                }
                catch (UnauthorizedAccessException) { _logger.Warn("Acesso negado: " + dir); }
                catch (PathTooLongException)        { _logger.Warn("Caminho muito longo: " + dir); }
                catch (Exception ex)               { _logger.Warn($"Aviso ao varrer '{dir}': {ex.Message}"); }
            }
        }

        private static void IndexFile(
            string filePath,
            string extLower,
            Dictionary<string, List<IndexedFile>> exactIndex,
            Dictionary<string, List<IndexedFile>> x0Index,
            List<IndexedFile> allFiles)
        {
            string baseName = Path.GetFileNameWithoutExtension(filePath)
                                  .Trim()
                                  .ToUpperInvariant();

            if (baseName.Length == 0) return;

            // Adiciona à lista completa (usada pela busca por prefixo)
            allFiles.Add(new IndexedFile(
                FullPath:  filePath,
                ExactKey:  baseName,
                X0Key:     CodeMatcher.NormalizeSchneiderToX0(baseName),
                Extension: extLower));

            // ── PASSO 1: indexa o nome-base COMPLETO ────────────────────
            // Necessário para nomes como "LV-1716_2.6" que contêm separadores
            // mas devem ser tratados como um código único, não quebrado em tokens.
            // Ex: "LV-1716_2.6" → chave exata "LV-1716_2.6"
            //                   → chave X0    "LV-1716_2.6" (não é padrão Schneider)
            {
                string x0Full = CodeMatcher.NormalizeSchneiderToX0(baseName);
                var entryFull = new IndexedFile(
                    FullPath:  filePath,
                    ExactKey:  baseName,
                    X0Key:     x0Full,
                    Extension: extLower);
                AddToIndex(exactIndex, baseName, entryFull);
                AddToIndex(x0Index,   x0Full,   entryFull);
            }

            // ── PASSO 2: indexa cada TOKEN do nome-base ──────────────────
            // FileBaseTokens agora NÃO quebra por traço — preserva nomes como
            // "51238828F002-02" inteiros. Só quebra por espaço e ponto.
            foreach (string token in CodeMatcher.FileBaseTokens(baseName))
            {
                if (token.Length == 0) continue;

                string x0Key = CodeMatcher.NormalizeSchneiderToX0(token);

                var entry = new IndexedFile(
                    FullPath:  filePath,
                    ExactKey:  token,
                    X0Key:     x0Key,
                    Extension: extLower);

                AddToIndex(exactIndex, token, entry);
                AddToIndex(x0Index,   x0Key, entry);

                // ── PASSO 3: para tokens com BASE-REVISAO, indexa também pela base ──
                // Ex: token "51238828F002-02" → indexa também por "51238828F002"
                // Assim a busca por base encontra todas as revisões disponíveis.
                var parsed = CodeMatcher.ExtractBaseAndRevision(token);
                if (parsed.HasValue)
                {
                    string baseKey = parsed.Value.Base;
                    var baseEntry = new IndexedFile(
                        FullPath:  filePath,
                        ExactKey:  baseKey,
                        X0Key:     baseKey,
                        Extension: extLower);
                    AddToIndex(exactIndex, baseKey, baseEntry);
                    AddToIndex(x0Index,   baseKey, baseEntry);
                }
            }
        }

        private static void AddToIndex(
            Dictionary<string, List<IndexedFile>> index,
            string key,
            IndexedFile entry)
        {
            if (!index.TryGetValue(key, out var list))
            {
                list = new List<IndexedFile>();
                index[key] = list;
            }
            list.Add(entry);
        }
    }
}

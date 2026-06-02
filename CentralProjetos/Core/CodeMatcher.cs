using System;
using System.Collections.Generic;
using System.Linq;
using DrawingCollector.Core.Models;

namespace DrawingCollector.Core
{
    /// <summary>
    /// Regras de correspondência entre códigos e nomes de arquivos.
    /// </summary>
    /// <remarks>
    /// Três categorias de código:
    ///
    /// 1. CÓDIGO LV — termina com "_LV" OU começa com "LV-"
    ///    Busca exata. Ex: "LV-0426-J" → só aceita arquivo "LV-0426-J"
    ///
    /// 2. CÓDIGO SCHNEIDER — contém "X" seguido de letra (XA, XB, XD...)
    ///    Normaliza para X0. Ex: "51132814XD" → aceita qualquer do grupo X0
    ///
    /// 3. CÓDIGO TRAÇO-REVISÃO — base fixa + traço + número de revisão
    ///    Ex: "51238828F002-02" → base="51238828F002", revisão=2
    ///    Busca por base, pega a maior revisão disponível.
    ///    Ex: "3729742" → busca exata (sem traço = sem revisão)
    /// </remarks>
    public static class CodeMatcher
    {
        private static readonly char[] TokenSeparators = new[] { ' ', '.' };

        // ════════════════════════════════════════════════════════════════
        //   CLASSIFICAÇÃO DO CÓDIGO
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Verifica se um código é interno LV:
        /// - Termina com "_LV" (ex: "ABC123_LV")
        /// - Começa com "LV-" (ex: "LV-0426-J", "LV-0427")
        /// </summary>
        public static bool IsLvCode(string? codeRaw)
        {
            if (string.IsNullOrWhiteSpace(codeRaw)) return false;
            string upper = codeRaw.Trim().ToUpperInvariant();
            return upper.Contains("_LV") || upper.StartsWith("LV-");
        }

        /// <summary>
        /// Verifica se um código segue o padrão Schneider (tem X seguido de letra).
        /// Ex: "51132814XD" → true. "51238828F002" → false.
        /// </summary>
        public static bool IsSchneiderCode(string? codeRaw)
        {
            if (string.IsNullOrWhiteSpace(codeRaw)) return false;
            string upper = codeRaw.Trim().ToUpperInvariant();
            if (IsLvCode(upper)) return false;

            int x = upper.LastIndexOf('X');
            if (x < 0 || x + 1 >= upper.Length) return false;
            char after = upper[x + 1];
            return after >= 'A' && after <= 'Z';
        }

        /// <summary>
        /// Para códigos com padrão "BASE-REVISAO":
        ///   - BASE-NUMERO:  "51238828F002-02", "3736808-01"
        ///   - BASE-LETRA:   "3729054-J", "3736815-C"
        /// Extrai a base e a revisão como string.
        /// Retorna null se não tiver traço-revisão no final.
        /// </summary>
        public static (string Base, string Revision)? ExtractBaseAndRevision(string? codeRaw)
        {
            if (string.IsNullOrWhiteSpace(codeRaw)) return null;
            string upper = codeRaw.Trim().ToUpperInvariant();

            // Não trata códigos LV ou Schneider aqui
            if (IsLvCode(upper) || IsSchneiderCode(upper)) return null;

            // Procura o ÚLTIMO traço
            int lastDash = upper.LastIndexOf('-');
            if (lastDash <= 0 || lastDash >= upper.Length - 1) return null;

            string suffix   = upper.Substring(lastDash + 1);
            string baseCode = upper.Substring(0, lastDash);

            // Sufixo deve ser letras e/ou dígitos (ex: "J", "C", "01", "02")
            if (suffix.Length == 0 || !suffix.All(c => char.IsLetterOrDigit(c))) return null;
            if (baseCode.Length == 0) return null;

            return (baseCode, suffix);
        }

        // ════════════════════════════════════════════════════════════════
        //   NORMALIZAÇÃO
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Normaliza um código Schneider para a revisão X0.
        /// Ex: "ABC123XD" → "ABC123X0". LV e não-Schneider não são afetados.
        /// </summary>
        public static string NormalizeSchneiderToX0(string? codeRaw)
        {
            string code = (codeRaw ?? "").Trim().ToUpperInvariant();
            if (code.Length == 0 || IsLvCode(code)) return code;

            int x = code.LastIndexOf('X');
            if (x >= 0 && x + 1 < code.Length)
            {
                char after = code[x + 1];
                if (after >= 'A' && after <= 'Z')
                {
                    var chars = code.ToCharArray();
                    chars[x + 1] = '0';
                    code = new string(chars);
                }
            }
            return code;
        }

        // ════════════════════════════════════════════════════════════════
        //   TOKENS DO NOME DE ARQUIVO
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Quebra o nome-base em tokens por espaço e ponto.
        /// Nota: NÃO quebra por traço aqui — o traço é tratado separadamente
        /// para preservar códigos como "LV-0426-J" e "51238828F002-02" inteiros.
        /// </summary>
        public static IEnumerable<string> FileBaseTokens(string? fileBaseRaw)
        {
            string name = (fileBaseRaw ?? "").Trim().ToUpperInvariant();
            if (name.Length == 0) yield break;

            foreach (var t in name.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries))
                yield return t;
        }

        // ════════════════════════════════════════════════════════════════
        //   MATCHING
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Verifica se um nome-base de arquivo corresponde a um código.
        /// Aplica a regra correta conforme o tipo do código.
        /// </summary>
        public static bool AutoMatches(string? codeRaw, string? fileBaseRaw)
        {
            if (string.IsNullOrWhiteSpace(codeRaw) || string.IsNullOrWhiteSpace(fileBaseRaw))
                return false;

            string codeIn   = codeRaw.Trim().ToUpperInvariant();
            string fileName = fileBaseRaw.Trim().ToUpperInvariant();

            // ── Regra 1: Código LV → match exato no nome completo ou token ──
            if (IsLvCode(codeIn))
            {
                // Aceita nome completo idêntico OU como token (separado por espaço/ponto)
                if (string.Equals(fileName, codeIn, StringComparison.Ordinal)) return true;
                return FileBaseTokens(fileName).Any(t =>
                    string.Equals(t, codeIn, StringComparison.Ordinal));
            }

            // ── Regra 2: Código Schneider → normaliza para X0 ──────────────
            if (IsSchneiderCode(codeIn))
            {
                string normalized = NormalizeSchneiderToX0(codeIn);
                // Verifica no nome completo e nos tokens
                if (MatchesSchneider(normalized, fileName)) return true;
                return FileBaseTokens(fileName).Any(t => MatchesSchneider(normalized, t));
            }

            // ── Regra 3: Código com BASE-REVISAO ──────────────────────────
            var parsed = ExtractBaseAndRevision(codeIn);
            if (parsed.HasValue)
            {
                string baseCode = parsed.Value.Base;
                // Aceita arquivo cujo nome começa com a base
                // Ex: código "51238828F002-02" → aceita "51238828F002-01", "51238828F002-02"
                if (FileNameMatchesBase(fileName, baseCode)) return true;
                return FileBaseTokens(fileName).Any(t => FileNameMatchesBase(t, baseCode));
            }

            // ── Regra 4: Código simples → match exato no nome completo ou token ──
            if (string.Equals(fileName, codeIn, StringComparison.Ordinal)) return true;
            return FileBaseTokens(fileName).Any(t =>
                string.Equals(t, codeIn, StringComparison.Ordinal));
        }

        /// <summary>
        /// Versão X0-only: para PDF/DWG onde só queremos revisão X0.
        /// </summary>
        public static bool AutoMatchesX0Only(string? codeRaw, string? fileBaseRaw)
        {
            if (string.IsNullOrWhiteSpace(codeRaw) || string.IsNullOrWhiteSpace(fileBaseRaw))
                return false;

            string codeIn   = codeRaw.Trim().ToUpperInvariant();
            string fileName = fileBaseRaw.Trim().ToUpperInvariant();

            // LV: exato
            if (IsLvCode(codeIn))
            {
                if (string.Equals(fileName, codeIn, StringComparison.Ordinal)) return true;
                return FileBaseTokens(fileName).Any(t =>
                    string.Equals(t, codeIn, StringComparison.Ordinal));
            }

            // Schneider X0: só aceita o normalizado exato
            if (IsSchneiderCode(codeIn))
            {
                string normalized = NormalizeSchneiderToX0(codeIn);
                if (string.Equals(fileName, normalized, StringComparison.Ordinal)) return true;
                return FileBaseTokens(fileName).Any(t =>
                    string.Equals(t, normalized, StringComparison.Ordinal));
            }

            // BASE-REVISAO e simples: mesmo comportamento do AutoMatches
            return AutoMatches(codeRaw, fileBaseRaw);
        }

        // ════════════════════════════════════════════════════════════════
        //   HELPERS PRIVADOS
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Verifica se um token bate com o código Schneider normalizado
        /// (X0 exato ou qualquer revisão do grupo).
        /// </summary>
        private static bool MatchesSchneider(string normalizedX0, string token)
        {
            if (string.Equals(token, normalizedX0, StringComparison.Ordinal)) return true;

            // Aceita variantes: mesmo prefixo até o X, com qualquer letra/dígito após
            // Ex: normalizedX0="51132814X0", token="51132814XD" → aceita
            if (token.Length >= normalizedX0.Length)
            {
                int xPos = normalizedX0.LastIndexOf('X');
                if (xPos >= 0 &&
                    string.Compare(token, 0, normalizedX0, 0, xPos + 1,
                                   StringComparison.Ordinal) == 0)
                {
                    // O que vem após o X no token pode ser letra ou dígito (revisão)
                    char afterX = token.Length > xPos ? token[xPos + 1] : '\0';
                    if (char.IsLetterOrDigit(afterX) &&
                        // Depois da letra de revisão pode ter sufixo ou nada
                        (token.Length == xPos + 2 ||
                         token[xPos + 2] == '-' ||
                         token[xPos + 2] == ' '))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Verifica se um nome de arquivo começa com a base do código
        /// seguida de traço+revisão (dígitos OU letras) ou fim de string.
        /// Ex: base="3729054",      fileName="3729054-J"        → true
        /// Ex: base="3736815",      fileName="3736815-C"        → true
        /// Ex: base="51238828F002", fileName="51238828F002-02"  → true
        /// Ex: base="51238828F002", fileName="51238828F002"     → true
        /// Ex: base="51238828F002", fileName="51238828F002X"    → false
        /// </summary>
        private static bool FileNameMatchesBase(string fileName, string baseCode)
        {
            if (!fileName.StartsWith(baseCode, StringComparison.Ordinal)) return false;
            if (fileName.Length == baseCode.Length) return true; // exato

            char next = fileName[baseCode.Length];
            if (next != '-') return false;

            // O que vem após o traço deve ser letras e/ou dígitos (revisão)
            string suffix = fileName.Substring(baseCode.Length + 1);
            return suffix.Length > 0 && suffix.All(c => char.IsLetterOrDigit(c));
        }

        // ════════════════════════════════════════════════════════════════
        //   SELEÇÃO DO MELHOR MATCH (maior revisão)
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Para códigos BASE-REVISAO, dentre vários arquivos encontrados,
        /// retorna o de "maior" revisão.
        /// Revisões numéricas têm prioridade sobre letras.
        /// Ex: ["3729054-J.pdf", "3729054.pdf"] → retorna o -J
        /// Ex: ["51238828F002-01.pdf", "51238828F002-02.pdf"] → retorna o -02
        /// </summary>
        public static string? SelectHighestRevision(IEnumerable<string> filePaths, string baseCode)
        {
            string? best      = null;
            string  bestRev   = string.Empty;
            bool    bestIsNum = false;
            string  basUp     = baseCode.ToUpperInvariant();

            foreach (var path in filePaths)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(path)
                                            .Trim().ToUpperInvariant();

                string rev;
                if (string.Equals(name, basUp, StringComparison.Ordinal))
                {
                    rev = string.Empty; // sem revisão = prioridade mínima
                }
                else if (name.StartsWith(basUp, StringComparison.Ordinal) &&
                         name.Length > basUp.Length && name[basUp.Length] == '-')
                {
                    rev = name.Substring(basUp.Length + 1);
                    if (!rev.All(c => char.IsLetterOrDigit(c))) continue;
                }
                else continue;

                if (best == null)
                {
                    best      = path;
                    bestRev   = rev;
                    bestIsNum = rev.All(char.IsDigit);
                    continue;
                }

                bool curIsNum = rev.All(char.IsDigit);

                // Numérico sempre ganha de letra/vazio
                if (curIsNum && !bestIsNum)
                {
                    best = path; bestRev = rev; bestIsNum = true; continue;
                }
                if (!curIsNum && bestIsNum) continue;

                // Ambos numéricos: compara valor
                if (curIsNum && bestIsNum)
                {
                    if (int.TryParse(rev, out int r) && int.TryParse(bestRev, out int br) && r > br)
                    { best = path; bestRev = rev; }
                    continue;
                }

                // Ambos letra/alfanumérico: compara string
                if (string.Compare(rev, bestRev, StringComparison.Ordinal) > 0)
                { best = path; bestRev = rev; bestIsNum = false; }
            }

            return best ?? filePaths.FirstOrDefault();
        }

        // ════════════════════════════════════════════════════════════════
        //   DEMAIS MÉTODOS (inalterados)
        // ════════════════════════════════════════════════════════════════

        public static MatchStrategy GetStrategyForExt(string? extLower) => extLower switch
        {
            ".pdf" or ".dwg" => MatchStrategy.SchneiderX0Only,
            ".stp" or ".dxf" => MatchStrategy.ExactOnly,
            _                => MatchStrategy.ExactThenSchneider,
        };

        public static int ExtractRevisionFromToken(string token, string normalizedX0)
        {
            if (string.Equals(token, normalizedX0, StringComparison.Ordinal))
                return -1;

            if (token.Length > normalizedX0.Length &&
                string.Compare(token, 0, normalizedX0, 0, normalizedX0.Length,
                               StringComparison.Ordinal) == 0)
            {
                string rest = token.Substring(normalizedX0.Length);
                if (rest.Length > 0 && rest[0] == '-') rest = rest.Substring(1);
                if (rest.Length > 0 && rest.All(char.IsDigit) &&
                    int.TryParse(rest, out int rev))
                    return rev;
            }
            return int.MinValue;
        }

        public sealed class MatchComparer : IComparer<string?>
        {
            private readonly string _normalizedX0;

            public MatchComparer(string? normalizedX0)
                => _normalizedX0 = normalizedX0 ?? string.Empty;

            public int Compare(string? x, string? y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (x is null) return -1;
                if (y is null) return 1;

                int dx = Math.Abs(x.Trim().Length - _normalizedX0.Length);
                int dy = Math.Abs(y.Trim().Length - _normalizedX0.Length);
                int cmp = dx.CompareTo(dy);

                return cmp != 0
                    ? cmp
                    : string.Compare(x.Trim(), y.Trim(), StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DrawingCollector.Core.Models
{
    /// <summary>
    /// Índice de arquivos com dois níveis de lookup: exato e X0-normalizado.
    /// </summary>
    /// <remarks>
    /// ANTES (R01–R03): um único Dictionary&lt;string, List&lt;string&gt;&gt;
    ///   chave = nome-base original → valor = lista de caminhos
    ///   Busca Schneider: varria TODOS os 50k arquivos para cada código. O(N×M).
    ///
    /// AGORA (R04): dois dicionários construídos uma única vez:
    ///
    ///   _exactIndex["51132814XD"]  → ["C:\...\51132814XD.dwg", ...]
    ///   _x0Index["51132814X0"]     → ["C:\...\51132814XD.dwg",
    ///                                  "C:\...\51132814XA.dwg",
    ///                                  "C:\...\51132814XB-01.dwg", ...]
    ///
    /// Busca Schneider agora: lookup direto em _x0Index. O(1) por código.
    ///
    /// Analogia: é a diferença entre um dicionário sem índice (folhear tudo)
    /// e um com índice alfabético E índice remissivo (vai direto na palavra).
    /// </remarks>
    public class FileIndex
    {
        // Chave = token exato em maiúsculas  →  arquivos com aquele token exato
        private readonly Dictionary<string, List<IndexedFile>> _exactIndex;

        // Chave = token normalizado X0 em maiúsculas  →  todos os arquivos do grupo
        private readonly Dictionary<string, List<IndexedFile>> _x0Index;

        // Todos os arquivos indexados (para busca por prefixo no nome completo)
        private readonly List<IndexedFile> _allFiles;

        /// <summary>Quantidade de nomes-base únicos no índice exato.</summary>
        public int UniqueExactKeys => _exactIndex.Count;

        /// <summary>Quantidade de grupos X0 únicos.</summary>
        public int UniqueX0Groups => _x0Index.Count;

        internal FileIndex(
            Dictionary<string, List<IndexedFile>> exactIndex,
            Dictionary<string, List<IndexedFile>> x0Index,
            List<IndexedFile> allFiles)
        {
            _exactIndex = exactIndex;
            _x0Index    = x0Index;
            _allFiles   = allFiles;
        }

        // ════════════════════════════════════════════════════════════════
        //   MÉTODOS DE BUSCA
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Busca exata: o token do arquivo deve ser idêntico ao código.
        /// O(1) — lookup direto no dicionário.
        /// </summary>
        public IReadOnlyList<string> FindExact(string code, string extLower)
        {
            string key = code.Trim().ToUpperInvariant();
            if (!_exactIndex.TryGetValue(key, out var files))
                return Array.Empty<string>();

            return files
                .Where(f => f.Extension == extLower)
                .Select(f => f.FullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Busca por prefixo: o nome do arquivo deve COMEÇAR com o código,
        /// seguido de fim de string, espaço, ou traço+texto-não-numérico.
        ///
        /// Isso garante que "ABM400014-05" ache "abm400014-05.dwg" mas NÃO
        /// "abm400014-20.dwg" (código diferente), e que "LV-0427" ache
        /// "LV-0427 - SUPORTE TC.dwg" (código + descrição).
        /// </summary>
        public IReadOnlyList<string> FindByPrefix(string code, string extLower)
        {
            string codeUp = code.Trim().ToUpperInvariant();

            var result = new List<string>();
            foreach (var f in _allFiles)
            {
                if (f.Extension != extLower) continue;

                // Compara o nome-base completo do arquivo
                string name = System.IO.Path.GetFileNameWithoutExtension(f.FullPath)
                                            .Trim().ToUpperInvariant();

                if (NameMatchesCode(name, codeUp))
                    result.Add(f.FullPath);
            }

            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Verifica se o nome do arquivo corresponde ao código exato,
        /// permitindo apenas sufixo descritivo separado por espaço.
        ///
        /// Aceita:  "ABM400014-05"          == "ABM400014-05"        → true
        ///          "LV-0427 - SUPORTE TC"  código + " - descrição"  → true
        ///          "LV-0480 REV1"          código + " descrição"    → true
        /// Rejeita: "ABM400014-20"          código diferente         → false
        ///          "ABM400014-051"         número maior             → false
        /// </summary>
        private static bool NameMatchesCode(string fileName, string code)
        {
            // Caso 1: nome idêntico ao código
            if (string.Equals(fileName, code, StringComparison.Ordinal))
                return true;

            // Caso 2: nome começa com o código seguido de ESPAÇO
            // (sufixo descritivo como "LV-0427 - SUPORTE" ou "LV-0480 REV1")
            // O espaço é o separador seguro: garante que o código terminou ali.
            if (fileName.StartsWith(code + " ", StringComparison.Ordinal))
                return true;

            return false;
        }

        /// <summary>
        /// Busca por base+revisão: encontra arquivos cujo nome é a base exata
        /// OU base seguida de "-revisão" (revisão = letras/dígitos).
        ///
        /// Usado para códigos que começam com dígito (a revisão vem do servidor).
        /// Ex: base "3729054" → acha "3729054-J", "3729054-01", "3729054"
        /// Ex: base "3736808" → acha "3736808-01", "3736808-02"
        ///
        /// NÃO confunde códigos diferentes: base "3729054" não acha "37290549".
        /// </summary>
        public IReadOnlyList<string> FindByBaseRevision(string baseCode, string extLower)
        {
            string baseUp = baseCode.Trim().ToUpperInvariant();

            var result = new List<string>();
            foreach (var f in _allFiles)
            {
                if (f.Extension != extLower) continue;

                string name = System.IO.Path.GetFileNameWithoutExtension(f.FullPath)
                                            .Trim().ToUpperInvariant();

                // Nome idêntico à base
                if (string.Equals(name, baseUp, StringComparison.Ordinal))
                {
                    result.Add(f.FullPath);
                    continue;
                }

                // Nome = base + "-" + revisão (revisão alfanumérica)
                if (name.StartsWith(baseUp + "-", StringComparison.Ordinal))
                {
                    string suffix = name.Substring(baseUp.Length + 1);
                    // A revisão pode ter descrição após espaço: "3729054-J DESC"
                    string revPart = suffix.Split(' ')[0];
                    if (revPart.Length > 0 && revPart.All(char.IsLetterOrDigit))
                        result.Add(f.FullPath);
                }
            }

            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Busca por grupo X0: aceita qualquer revisão do mesmo "pai".
        /// O(1) — lookup direto no índice X0.
        ///
        /// Exemplo: código "51132814XD" → normaliza para "51132814X0"
        /// → retorna todos os arquivos do grupo: X0, XA, XB, XD, X1...
        /// </summary>
        public IReadOnlyList<string> FindByX0(string code, string extLower)
        {
            string x0Key = CodeMatcher.NormalizeSchneiderToX0(
                               code.Trim().ToUpperInvariant());

            if (!_x0Index.TryGetValue(x0Key, out var files))
                return Array.Empty<string>();

            return files
                .Where(f => f.Extension == extLower)
                .Select(f => f.FullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Busca para código LV (_LV): sempre exata, nunca normaliza.
        /// </summary>
        public IReadOnlyList<string> FindLv(string code, string extLower)
            => FindByPrefix(code, extLower);
    }
}

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DrawingCollector.Core.Models;

namespace DrawingCollector.Core
{
    /// <summary>
    /// Salva e carrega as configurações do programa em JSON.
    /// </summary>
    /// <remarks>
    /// ONDE fica o arquivo:
    ///   C:\Users\[usuario]\AppData\Roaming\DrawingCollector\config.json
    ///
    ///   Em código: Environment.GetFolderPath(SpecialFolder.ApplicationData)
    ///              + "\DrawingCollector\config.json"
    ///
    /// POR QUE JSON e não XML ou INI?
    ///   - Legível por humanos (você pode abrir e editar no Bloco de Notas)
    ///   - Suporte nativo no .NET 8 (System.Text.Json) sem pacote extra
    ///   - Fácil de versionar e comparar
    ///
    /// POR QUE não usar Properties.Settings (o padrão antigo do WinForms)?
    ///   - Properties.Settings é compilado junto ao assembly — difícil de
    ///     transferir entre máquinas e impossível de editar manualmente
    ///   - JSON em AppData é portável e transparente
    /// </remarks>
    public static class SettingsManager
    {
        // Caminho completo do arquivo de configuração
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DrawingCollector",
            "config.json");

        // Opções do serializador JSON: indentado (legível), ignora nulos
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented         = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>
        /// Carrega as configurações do arquivo JSON.
        /// Se o arquivo não existir (primeira execução), retorna configurações padrão.
        /// Se o arquivo existir mas estiver corrompido, retorna padrão e loga o erro.
        /// </summary>
        public static AppSettings Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                    return new AppSettings(); // primeira execução → padrões

                string json = File.ReadAllText(ConfigPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);

                // Se desserializou mas retornou null (JSON era "null"), usa padrão
                return settings ?? new AppSettings();
            }
            catch (JsonException)
            {
                // JSON corrompido → ignora e usa padrão
                // Não lançamos exceção pois é melhor abrir com padrão do que travar
                return new AppSettings();
            }
            catch (IOException)
            {
                // Sem acesso ao arquivo → padrão
                return new AppSettings();
            }
        }

        /// <summary>
        /// Salva as configurações no arquivo JSON.
        /// Cria a pasta se não existir.
        /// Falhas silenciosas — não deve impedir o fechamento do programa.
        /// </summary>
        public static void Save(AppSettings settings)
        {
            try
            {
                // Garante que a pasta existe
                string? dir = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(settings, JsonOptions);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception)
            {
                // Falha ao salvar não deve travar o programa
                // Em produção, logar aqui seria ideal
            }
        }

        /// <summary>
        /// Retorna o caminho do config.json para exibir em diagnóstico.
        /// </summary>
        public static string GetConfigPath() => ConfigPath;
    }
}

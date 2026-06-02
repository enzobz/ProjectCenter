# DrawingCollector — R02

Refatoração da R01: mesmo comportamento, código organizado em arquivos separados.

---

## Estrutura do projeto

```
DrawingCollector/
├── Program.cs                       ← ponto de entrada (só o Main)
│
├── UI/
│   ├── MainForm.cs                  ← janela principal
│   ├── Controls/
│   │   ├── RoundedButton.cs         ← botão com cantos arredondados
│   │   └── GradientHeader.cs        ← cabeçalho com gradiente
│   ├── Theme/
│   │   ├── Palette.cs               ← TODAS as cores em um lugar só
│   │   └── ThemeManager.cs          ← lógica de tema claro/escuro
│   └── Extensions/
│       └── ControlExtensions.cs     ← SetDoubleBuffered
│
├── Core/
│   ├── CodeMatcher.cs               ← regras Schneider/LV (pura, testável)
│   ├── FileIndexer.cs               ← varre pastas e constrói índice
│   ├── DrawingCollectorService.cs   ← orquestra: indexa → busca → copia
│   └── Models/
│       ├── CollectorOptions.cs      ← opções de uma execução
│       ├── MatchStrategy.cs         ← enum das estratégias de busca
│
├── Reports/
│   ├── ExcelReporter.cs             ← gera .xlsx de faltantes (ClosedXML)
│   └── ZipReporter.cs               ← gera .zip do destino
│
└── Logging/
    ├── ILogger.cs                   ← contrato: Info/Warn/Ok/Err
    └── RichTextBoxLogger.cs         ← implementação: escreve no log da UI
```

---

## O que mudou da R01 para a R02

| Item | R01 | R02 |
|------|-----|-----|
| Arquivos | 1 (`Program.cs`, 1532 linhas) | 14 arquivos, cada um com ≤ 300 linhas |
| Namespace | `DrawingCollectorCompat` | `DrawingCollector` |
| Excel | COM Interop (exige Excel instalado) | ClosedXML (sem dependência) |
| Cores | `Color.FromArgb(...)` espalhado | Centralizadas em `Palette.cs` |
| Log | Métodos dentro do `MainForm` | Classe `RichTextBoxLogger` isolada |
| Tema | Varredor genérico recursivo | `ThemeManager` com tipos explícitos |
| `catch {}` | 11 ocorrências vazias | Exceções específicas com comentário |
| `int.Parse` sem proteção | Sim | Substituído por `int.TryParse` |
| ZIP | Copia tudo para pasta temp, depois zipa | Adiciona direto no ZIP (sem temp) |
| Constantes de coluna | Strings mágicas (`"Sel"`, `"Codigo"`) | Classe `GridCol` com constantes |
| `if (InvokeRequired) Invoke(...)` | Repetido 8 vezes | Método `SafeInvoke` centralizado |

---

## Como compilar

```powershell
cd DrawingCollector
dotnet restore          # baixa o ClosedXML e dependências
dotnet build            # compila em Debug
dotnet publish -c Release -r win-x64 --self-contained true
```

O executável fica em `bin\Release\net8.0-windows\win-x64\publish\DrawingCollector.exe`

---

## Próximas etapas planejadas

- **Etapa 2:** Entrada de códigos (colar direto + .txt + .xlsx)
- **Etapa 3:** Indexação por X0 normalizado (performance)
- **Etapa 4:** Modo Barramento + banco Excel (TemDobra: Sim/Não)
- **Etapa 5:** Robustez e UX (cancelamento, persistência de config)
- **Etapa 6:** Testes automatizados das regras de matching

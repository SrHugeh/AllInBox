# All In Box

**Game Launcher & Library Organizer** para Windows. Centralize os jogos de todas as suas plataformas em um só lugar.

## Recursos

- **Biblioteca unificada**: veja seus jogos em grade (com ícone em destaque) ou em tabela, com busca instantânea e uma prateleira "Continuar jogando".
- **Escaneamento automático**: detecta jogos instalados de Steam, Epic Games, GOG, Ubisoft, Riot Games e Minecraft, ou de qualquer pasta que você escolher.
- **Adição manual e arrastar e soltar**: cadastre qualquer `.exe`, `.bat` ou atalho `.lnk`, ou solte arquivos e pastas direto na janela. Dá para definir ícone, categoria, argumentos de inicialização, diretório de trabalho e anotações.
- **Favoritos e recentes**: marque seus preferidos e volte rápido ao que jogou por último.
- **Ocultar o que não é jogo**: utilitários detectados pelo escaneamento (Aseprite, Soundpad, Wallpaper Engine etc.) são ocultados uma única vez, e você pode ocultar ou restaurar qualquer item. Nada é apagado: os ocultos ficam na aba **Ocultos**.
- **Ordenação flexível**: por nome, mais jogados, recentemente jogados, tamanho, data de adição, favoritos ou plataforma.
- **Estatísticas**: espaço em disco, tempo total de jogo, mais jogados, mais pesados, distribuição por plataforma e backlog.
- **Bandeja do sistema**: menu com seus jogos recentes para iniciar com um clique, sem abrir a janela.
- **Temas**: automático (segue o Windows), escuro, claro ou personalizado, com temas prontos e cores livres.
- **Multilíngue**: Português (Brasil), English, Español e 日本語, com opção de seguir o idioma do sistema.

> Remover um jogo da biblioteca **não** apaga os arquivos dele do seu computador.

## Download

Baixe o `.zip` mais recente na página de [**Releases**](https://github.com/SrHugeh/AllInBox/releases/latest), extraia e execute `AllInBox.exe`.

- Não é necessário instalar o .NET: o executável já inclui tudo.
- Mantenha as pastas `Web` e `Resources` ao lado do `AllInBox.exe` (elas contêm a interface e os idiomas).
- Requer Windows 10 ou 11 de 64 bits e o **Microsoft Edge WebView2 Runtime**. Ele já vem com o Windows 11 e com a maioria dos Windows 10 atualizados. Se faltar, o app avisa e indica o link de instalação: <https://developer.microsoft.com/microsoft-edge/webview2/>.

## Compilar a partir do código

Pré-requisitos: [.NET SDK 10](https://dotnet.microsoft.com/download) e Windows (o projeto usa WPF e WebView2).

```powershell
git clone https://github.com/SrHugeh/AllInBox.git
cd AllInBox
dotnet run
```

Para gerar um executável autocontido em arquivo único:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Distribua o `AllInBox.exe` junto com as pastas `Web` e `Resources` da saída da publicação.

## Como a interface funciona

A janela é um shell WPF (bandeja, lançamento de jogos, escaneamento, armazenamento) que hospeda uma interface em **HTML, CSS e JavaScript** dentro do WebView2. O C# e a página conversam por mensagens JSON (`chrome.webview.postMessage` e `PostWebMessageAsJson`), e as cores da página vêm direto do tema do aplicativo.

Para trabalhar só no visual, abra `Web/index.html` em um navegador: fora do app, ele carrega uma biblioteca de exemplo (`Web/dev-mock.js`, que não vai para a publicação). Atalhos úteis na URL:

| Parâmetro | Efeito |
|---|---|
| `?view=stats` / `favorites` / `recent` / `hidden` | abre a tela correspondente |
| `?mode=table` | visão em tabela |
| `?light=1` | tema claro |
| `?lang=en-US` | troca o idioma |
| `?modal=add` / `edit` / `scan` / `settings` / `confirm` | abre um modal |
| `?noanim=1` | desliga animações (útil para capturas de tela) |

### Testar sem mexer na sua biblioteca

O app guarda seus dados em `%LocalAppData%\AllInBox`. Para testar com segurança, aponte outra pasta antes de abrir o executável:

```powershell
$env:ALLINBOX_DATA_DIR = "C:\temp\allinbox-teste"
.\AllInBox.exe
```

## Estrutura do projeto

| Pasta | Conteúdo |
|---|---|
| `Web/` | Interface: `index.html`, `app.css`, `app.js` (núcleo), `modals.js` (novo/editar jogo, escanear, configurações, confirmar) e `vendor/` (bibliotecas) |
| `Services/` | Escaneamento, biblioteca, execução de jogos, ícones, idiomas, tema, configurações e bandeja |
| `Models/` | Modelos de dados |
| `Views/` | Janelas nativas restantes: popup da bandeja e diálogo de erros |
| `Resources/i18n/` | Arquivos de tradução (`pt-BR`, `en-US`, `es-ES`, `ja-JP`) |
| `Assets/` | Ícones e imagens |
| `MainWindow.*` | Janela principal e a ponte entre o C# e a interface web |

## Bibliotecas de terceiros

- [Lucide](https://lucide.dev) (ícones), licença ISC.
- [GSAP](https://gsap.com) (animações), sob a [licença padrão da GreenSock](https://gsap.com/standard-license).
- [Microsoft Edge WebView2](https://developer.microsoft.com/microsoft-edge/webview2/), para hospedar a interface web.

Os avisos de licença das bibliotecas embutidas estão em `Web/vendor/LICENSES.txt`.

## Contribuindo

Sugestões e pull requests são bem-vindos. Abra uma [issue](https://github.com/SrHugeh/AllInBox/issues) para relatar problemas ou propor melhorias. Para adicionar um idioma, copie um arquivo de `Resources/i18n/` e traduza os valores; os textos da interface web estão no início de `Web/app.js` e `Web/modals.js`.

## Licença

Distribuído sob a licença [MIT](LICENSE).

# All In Box

**Game Launcher & Library Organizer** para Windows. Centralize os jogos de todas as suas plataformas em um só lugar.

## Recursos

- **Biblioteca unificada**: veja todos os seus jogos em cards ou em lista, com busca instantânea.
- **Escaneamento automático**: detecta jogos instalados de Steam, Epic Games, GOG, Ubisoft, Riot Games e Minecraft, ou de qualquer pasta que você escolher.
- **Adição manual**: cadastre qualquer `.exe` ou atalho `.lnk`, com ícone, categoria, argumentos de inicialização, diretório de trabalho e anotações.
- **Favoritos e recentes**: marque seus preferidos e volte rápido ao que jogou por último.
- **Ordenação flexível**: por nome, mais jogados, recentemente jogados, tamanho, data de adição, favoritos ou plataforma.
- **Estatísticas**: tempo total de jogo, jogos mais jogados, backlog (nunca jogados), distribuição por plataforma e resumo da conta.
- **Bandeja do sistema**: o app continua acessível pelo ícone na bandeja.
- **Temas**: serviço de tema integrado.
- **Multilíngue**: Português (Brasil), English, Español e 日本語, com opção de seguir o idioma do sistema.

> Remover um jogo da caixa **não** apaga os arquivos dele do seu computador.

## Download

Baixe o `.zip` mais recente na página de [**Releases**](https://github.com/SrHugeh/AllInBox/releases/latest), extraia e execute `AllInBox.exe`.

- Não é necessário instalar o .NET: o executável já inclui tudo.
- Mantenha a pasta `Resources` ao lado do `AllInBox.exe` (ela contém os idiomas).
- Requer Windows 10 ou 11 de 64 bits.

## Compilar a partir do código

Pré-requisitos: [.NET SDK 10](https://dotnet.microsoft.com/download) e Windows (o projeto usa WPF).

```powershell
git clone https://github.com/SrHugeh/AllInBox.git
cd AllInBox
dotnet run
```

Para gerar um executável autocontido em arquivo único:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## Estrutura do projeto

| Pasta | Conteúdo |
|---|---|
| `Views/` | Janelas (adicionar/editar jogo, escanear, configurações, diálogos) |
| `Services/` | Escaneamento, biblioteca, execução de jogos, ícones, idiomas, tema e bandeja |
| `Models/` | Modelos de dados e de estatísticas |
| `Converters/` | Conversores de binding do WPF |
| `Resources/i18n/` | Arquivos de tradução (`pt-BR`, `en-US`, `es-ES`, `ja-JP`) |
| `Assets/` | Ícones e imagens |

## Contribuindo

Sugestões e pull requests são bem-vindos. Abra uma [issue](https://github.com/SrHugeh/AllInBox/issues) para relatar problemas ou propor melhorias. Para adicionar um idioma, copie um arquivo de `Resources/i18n/` e traduza os valores.

## Licença

Distribuído sob a licença [MIT](LICENSE).

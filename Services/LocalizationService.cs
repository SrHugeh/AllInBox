using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace AllInBox.Services;

public class LocalizationService
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance ??= new LocalizationService();

    public event Action? LanguageChanged;

    public string CurrentLanguageCode { get; private set; } = "pt-BR";

    private readonly Dictionary<string, Dictionary<string, string>> _translations = new(StringComparer.OrdinalIgnoreCase);

    public LocalizationService()
    {
        LoadAllTranslations();
        InitializeLanguage();
    }

    private void LoadAllTranslations()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string i18nDir = Path.Combine(baseDir, "Resources", "i18n");

        string[] supportedLanguages = { "pt-BR", "en-US", "es-ES", "ja-JP" };

        foreach (var lang in supportedLanguages)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            string filePath = Path.Combine(i18nDir, $"{lang}.json");
            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (loaded != null)
                    {
                        foreach (var kvp in loaded)
                        {
                            dict[kvp.Key] = kvp.Value;
                        }
                    }
                }
                catch { }
            }

            _translations[lang] = dict;
        }

        // Fallback default dictionaries if files not found
        EnsureFallbackTranslations();
    }

    private void EnsureFallbackTranslations()
    {
        if (!_translations.ContainsKey("pt-BR") || _translations["pt-BR"].Count == 0)
        {
            _translations["pt-BR"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["AppTitle"] = "All In Box - Central de Jogos",
                ["Biblioteca"] = "BIBLIOTECA",
                ["TodosJogos"] = "Todos os Jogos",
                ["Favoritos"] = "Favoritos",
                ["Recentes"] = "Recentes",
                ["Graficos"] = "Gráficos",
                ["Plataformas"] = "PLATAFORMAS",
                ["BuscarJogos"] = "Buscar jogos...",
                ["Ordenar"] = "Ordenar:",
                ["Sort_NameAsc"] = "Nome (A - Z)",
                ["Sort_NameDesc"] = "Nome (Z - A)",
                ["Sort_Recent"] = "Recentemente Jogados",
                ["Sort_MostPlayed"] = "Mais Jogados",
                ["Sort_DateAdded"] = "Data de Adição",
                ["Sort_Favorites"] = "Favoritos Primeiro",
                ["Sort_Platform"] = "Plataforma (A - Z)",
                ["Escanear"] = "Escanear",
                ["AdicionarJogo"] = "Adicionar Jogo",
                ["SelecionarTudo"] = "Selecionar tudo",
                ["DesmarcarTudo"] = "Desmarcar tudo",
                ["RemoverDaCaixa"] = "Remover da caixa",
                ["Jogar"] = "▶ JOGAR",
                ["EmJogo"] = "● EM JOGO",
                ["Iniciar"] = "Iniciar",
                ["NuncaJogado"] = "Nunca jogado",
                ["UltimaVez"] = "Última vez",
                ["Hoje"] = "Hoje",
                ["Ontem"] = "Ontem",
                ["DiasAtras"] = "dias atrás",
                ["Pronto"] = "Pronto",
                ["JogandoAgora"] = "Jogando agora: {0}",
                ["Configuracoes"] = "Configurações",
                ["Idioma"] = "Idioma",
                ["IdiomaDesc"] = "Selecione o idioma da interface do aplicativo:",
                ["IdiomaAuto"] = "Automático (Padrão do Sistema)",
                ["Fechar"] = "Fechar",
                ["Salvar"] = "Salvar",
                ["Cancelar"] = "Cancelar",
                ["Confirmar"] = "Confirmar",
                ["TempoTotal"] = "TEMPO TOTAL",
                ["TotalJogos"] = "TOTAL DE JOGOS",
                ["JogosIniciados"] = "JOGOS INICIADOS",
                ["Backlog"] = "BACKLOG",
                ["FavoritosKPI"] = "FAVORITOS",
                ["MaisJogados"] = "MAIS JOGADOS",
                ["NuncaJogadosBacklog"] = "NUNCA JOGADOS (BACKLOG)",
                ["JogosPorPlataforma"] = "JOGOS POR PLATAFORMA",
                ["ResumoDaConta"] = "RESUMO DA CONTA",
                ["PlataformaDominante"] = "Plataforma dominante:",
                ["MediaTempoJogado"] = "Média de tempo jogado:",
                ["TodosIniciados"] = "Todos os títulos da sua biblioteca já foram iniciados.",
                ["NenhumJogoJogado"] = "Nenhum jogo foi iniciado ainda.",
                ["ConfirmarRemocao"] = "Tem certeza que deseja remover '{0}' da sua caixa?\n\n(Os arquivos do jogo NÃO serão excluídos do seu computador)",
                ["Remover"] = "Remover",
                ["JogosImportados"] = "{0} jogos importados com sucesso para sua caixa!"
            };
        }

        if (!_translations.ContainsKey("ja-JP") || _translations["ja-JP"].Count == 0)
        {
            _translations["ja-JP"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["AppTitle"] = "All In Box - ゲームセンター",
                ["Biblioteca"] = "ライブラリ",
                ["TodosJogos"] = "すべてのゲーム",
                ["Favoritos"] = "お気に入り",
                ["Recentes"] = "最近プレイ",
                ["Graficos"] = "統計・グラフ",
                ["Plataformas"] = "プラットフォーム",
                ["BuscarJogos"] = "ゲームを検索...",
                ["Ordenar"] = "並び替え:",
                ["Sort_NameAsc"] = "名前 (A - Z)",
                ["Sort_NameDesc"] = "名前 (Z - A)",
                ["Sort_Recent"] = "最近プレイした順",
                ["Sort_MostPlayed"] = "プレイ時間順",
                ["Sort_DateAdded"] = "追加日順",
                ["Sort_Favorites"] = "お気に入りを優先",
                ["Sort_Platform"] = "プラットフォーム (A - Z)",
                ["Escanear"] = "スキャン",
                ["AdicionarJogo"] = "ゲームを追加",
                ["SelecionarTudo"] = "すべて選択",
                ["DesmarcarTudo"] = "選択解除",
                ["RemoverDaCaixa"] = "ボックスから削除",
                ["Jogar"] = "▶ プレイ",
                ["EmJogo"] = "● プレイ中",
                ["Iniciar"] = "起動",
                ["NuncaJogado"] = "未プレイ",
                ["UltimaVez"] = "最終プレイ",
                ["Hoje"] = "今日",
                ["Ontem"] = "昨日",
                ["DiasAtras"] = "日前",
                ["Pronto"] = "準備完了",
                ["JogandoAgora"] = "プレイ中: {0}",
                ["Configuracoes"] = "設定",
                ["Idioma"] = "言語",
                ["IdiomaDesc"] = "アプリケーションの表示言語を選択してください:",
                ["IdiomaAuto"] = "自動 (システムデフォルト)",
                ["Fechar"] = "閉じる",
                ["Salvar"] = "保存",
                ["Cancelar"] = "キャンセル",
                ["Confirmar"] = "確認",
                ["TempoTotal"] = "総プレイ時間",
                ["TotalJogos"] = "総ゲーム数",
                ["JogosIniciados"] = "起動済みゲーム",
                ["Backlog"] = "未プレイ (バックログ)",
                ["FavoritosKPI"] = "お気に入り",
                ["MaisJogados"] = "最もプレイしたゲーム",
                ["NuncaJogadosBacklog"] = "未プレイ (バックログ)",
                ["JogosPorPlataforma"] = "プラットフォーム別",
                ["ResumoDaConta"] = "アカウント概要",
                ["PlataformaDominante"] = "主要プラットフォーム:",
                ["MediaTempoJogado"] = "平均プレイ時間:",
                ["TodosIniciados"] = "ライブラリのすべてのゲームが既に起動されています。",
                ["NenhumJogoJogado"] = "まだ起動されたゲームはありません。",
                ["ConfirmarRemocao"] = "「{0}」をボックスから削除してもよろしいですか？\n\n(ゲームファイルはPCから削除されません)",
                ["Remover"] = "削除",
                ["JogosImportados"] = "{0} 本のゲームをボックスにインポートしました！"
            };
        }
    }

    public void InitializeLanguage()
    {
        var settings = AppSettings.Instance;
        if (string.Equals(settings.Language, "auto", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(settings.Language))
        {
            string systemCulture = CultureInfo.CurrentUICulture.Name;
            if (systemCulture.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
            {
                CurrentLanguageCode = "ja-JP";
            }
            else if (systemCulture.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            {
                CurrentLanguageCode = "en-US";
            }
            else if (systemCulture.StartsWith("es", StringComparison.OrdinalIgnoreCase))
            {
                CurrentLanguageCode = "es-ES";
            }
            else
            {
                CurrentLanguageCode = "pt-BR";
            }
        }
        else if (_translations.ContainsKey(settings.Language))
        {
            CurrentLanguageCode = settings.Language;
        }
        else
        {
            CurrentLanguageCode = "pt-BR";
        }
    }

    public void SetLanguage(string languageCode)
    {
        if (languageCode == "auto")
        {
            AppSettings.Instance.Language = "auto";
            AppSettings.Instance.Save();
            InitializeLanguage();
            LanguageChanged?.Invoke();
            return;
        }

        if (_translations.ContainsKey(languageCode))
        {
            CurrentLanguageCode = languageCode;
            AppSettings.Instance.Language = languageCode;
            AppSettings.Instance.Save();
            LanguageChanged?.Invoke();
        }
    }

    public string Get(string key, params object[] args)
    {
        if (_translations.TryGetValue(CurrentLanguageCode, out var dict) && dict.TryGetValue(key, out var text))
        {
            return args.Length > 0 ? string.Format(text, args) : text;
        }

        if (_translations.TryGetValue("pt-BR", out var ptDict) && ptDict.TryGetValue(key, out var fallbackText))
        {
            return args.Length > 0 ? string.Format(fallbackText, args) : fallbackText;
        }

        return key;
    }
}

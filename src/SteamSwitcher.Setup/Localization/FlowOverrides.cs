using System.Collections.Generic;

namespace SteamSwitcher.Setup.Localization;

/// <summary>
/// Copy and status corrections for the 0.6 guided flow.
/// Kept separate so the generated catalogs do not need to be rewritten.
/// </summary>
internal static class FlowOverrides
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Languages =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["es"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["WelcomeDoesNot4"] = "No mantiene dos sesiones de Steam abiertas a la vez.",
                ["RememberActive"] = "Recordar contraseña detectada",
                ["RememberInactive"] = "Recordar contraseña no detectada",
                ["RememberNone"] = "Sin cuenta detectada",
                ["PairReadyDetail"] = "Recordar contraseña detectada",
                ["StatusSteamActivated"] = "Steam ya estaba abierto. Se ha traído al frente.",
                ["StatusActivatingSteam"] = "Activando {0}…",
                ["StatusSteamActivationFailed"] = "Steam está abierto, pero no se pudo traer su ventana al frente. Ábrelo desde la bandeja de Windows.",
                ["AccountsTip"] = "El login, Steam Guard y Recordar contraseña se completan directamente en Steam. Si esta copia ya está abierta, se trae al frente; si no, se cierra la otra copia antes de abrirla."
            },
            ["en"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["WelcomeDoesNot4"] = "Does not keep two Steam sessions open at the same time.",
                ["RememberActive"] = "Remember password detected",
                ["RememberInactive"] = "Remember password not detected",
                ["RememberNone"] = "No account detected",
                ["PairReadyDetail"] = "Remember password detected",
                ["StatusSteamActivated"] = "Steam was already open. It has been brought to the front.",
                ["StatusActivatingSteam"] = "Activating {0}…",
                ["StatusSteamActivationFailed"] = "Steam is running, but its window could not be brought to the front. Open it from the Windows notification area.",
                ["AccountsTip"] = "Login, Steam Guard and Remember password are completed directly in Steam. If this copy is already open, it is brought to the front; otherwise, the other copy is closed before opening it."
            },
            ["zh"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["WelcomeDoesNot4"] = "不会同时保持两个 Steam 会话打开。",
                ["RememberActive"] = "已检测到记住密码",
                ["RememberInactive"] = "未检测到记住密码",
                ["RememberNone"] = "未检测到账户",
                ["PairReadyDetail"] = "已检测到记住密码",
                ["StatusSteamActivated"] = "Steam 已在运行，已将其置于前台。",
                ["StatusActivatingSteam"] = "正在激活 {0}…",
                ["StatusSteamActivationFailed"] = "Steam 正在运行，但无法将其窗口置于前台。请从 Windows 通知区域打开它。",
                ["AccountsTip"] = "登录、Steam Guard 和记住密码都在 Steam 中完成。如果该副本已打开，会将其置于前台；否则先关闭另一个副本再打开。"
            },
            ["hi"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["WelcomeDoesNot4"] = "एक समय में दो Steam सत्र खुले नहीं रखता।",
                ["RememberActive"] = "पासवर्ड याद रखें विकल्प मिला",
                ["RememberInactive"] = "पासवर्ड याद रखें विकल्प नहीं मिला",
                ["RememberNone"] = "कोई खाता नहीं मिला",
                ["PairReadyDetail"] = "पासवर्ड याद रखें विकल्प मिला",
                ["StatusSteamActivated"] = "Steam पहले से खुला था। उसे सामने लाया गया है।",
                ["StatusActivatingSteam"] = "{0} सक्रिय किया जा रहा है…",
                ["StatusSteamActivationFailed"] = "Steam चल रहा है, लेकिन उसकी विंडो सामने नहीं लाई जा सकी। इसे Windows सूचना क्षेत्र से खोलें।",
                ["AccountsTip"] = "लॉगिन, Steam Guard और पासवर्ड याद रखें सीधे Steam में पूरे होते हैं। अगर यह प्रति पहले से खुली है तो उसे सामने लाया जाता है; वरना दूसरी प्रति बंद करके इसे खोला जाता है।"
            },
            ["ar"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["WelcomeDoesNot4"] = "لا يحافظ على جلستي Steam مفتوحتين في الوقت نفسه.",
                ["RememberActive"] = "تم اكتشاف تذكّر كلمة المرور",
                ["RememberInactive"] = "لم يتم اكتشاف تذكّر كلمة المرور",
                ["RememberNone"] = "لم يتم اكتشاف حساب",
                ["PairReadyDetail"] = "تم اكتشاف تذكّر كلمة المرور",
                ["StatusSteamActivated"] = "كان Steam مفتوحاً بالفعل. تم إحضاره إلى المقدمة.",
                ["StatusActivatingSteam"] = "جارٍ تفعيل {0}…",
                ["StatusSteamActivationFailed"] = "Steam قيد التشغيل، لكن تعذر إحضار نافذته إلى المقدمة. افتحه من منطقة إشعارات Windows.",
                ["AccountsTip"] = "يتم تسجيل الدخول وSteam Guard وتذكّر كلمة المرور مباشرة في Steam. إذا كانت هذه النسخة مفتوحة فسيتم إحضارها إلى المقدمة؛ وإلا فسيتم إغلاق النسخة الأخرى قبل فتحها."
            },
            ["pt"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["WelcomeDoesNot4"] = "Não mantém duas sessões do Steam abertas ao mesmo tempo.",
                ["RememberActive"] = "Lembrar palavra-passe detetado",
                ["RememberInactive"] = "Lembrar palavra-passe não detetado",
                ["RememberNone"] = "Nenhuma conta detetada",
                ["PairReadyDetail"] = "Lembrar palavra-passe detetado",
                ["StatusSteamActivated"] = "O Steam já estava aberto. Foi trazido para a frente.",
                ["StatusActivatingSteam"] = "A ativar {0}…",
                ["StatusSteamActivationFailed"] = "O Steam está em execução, mas não foi possível trazer a janela para a frente. Abra-o a partir da área de notificação do Windows.",
                ["AccountsTip"] = "O login, o Steam Guard e o Lembrar palavra-passe são concluídos diretamente no Steam. Se esta cópia já estiver aberta, é trazida para a frente; caso contrário, a outra cópia é fechada antes de a abrir."
            },
            ["ru"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["WelcomeDoesNot4"] = "Не держит два сеанса Steam открытыми одновременно.",
                ["RememberActive"] = "Запоминание пароля обнаружено",
                ["RememberInactive"] = "Запоминание пароля не обнаружено",
                ["RememberNone"] = "Учётная запись не обнаружена",
                ["PairReadyDetail"] = "Запоминание пароля обнаружено",
                ["StatusSteamActivated"] = "Steam уже был открыт. Окно выведено на передний план.",
                ["StatusActivatingSteam"] = "Активация {0}…",
                ["StatusSteamActivationFailed"] = "Steam запущен, но не удалось вывести его окно на передний план. Откройте его из области уведомлений Windows.",
                ["AccountsTip"] = "Вход, Steam Guard и запоминание пароля выполняются непосредственно в Steam. Если эта копия уже открыта, её окно выводится на передний план; иначе другая копия закрывается перед открытием."
            },
            ["ja"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["WelcomeDoesNot4"] = "2つの Steam セッションを同時に開いたままにはしません。",
                ["RememberActive"] = "パスワードの記憶を検出",
                ["RememberInactive"] = "パスワードの記憶は未検出",
                ["RememberNone"] = "アカウントが検出されません",
                ["PairReadyDetail"] = "パスワードの記憶を検出",
                ["StatusSteamActivated"] = "Steam はすでに起動していました。前面に表示しました。",
                ["StatusActivatingSteam"] = "{0} をアクティブ化しています…",
                ["StatusSteamActivationFailed"] = "Steam は実行中ですが、ウィンドウを前面に表示できませんでした。Windows の通知領域から開いてください。",
                ["AccountsTip"] = "ログイン、Steam ガード、パスワードの記憶は Steam 内で完了します。このコピーがすでに開いている場合は前面に表示し、そうでない場合はもう一方を閉じてから開きます。"
            },
            ["fr"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["WelcomeDoesNot4"] = "Ne garde pas deux sessions Steam ouvertes en même temps.",
                ["RememberActive"] = "Mémorisation du mot de passe détectée",
                ["RememberInactive"] = "Mémorisation du mot de passe non détectée",
                ["RememberNone"] = "Aucun compte détecté",
                ["PairReadyDetail"] = "Mémorisation du mot de passe détectée",
                ["StatusSteamActivated"] = "Steam était déjà ouvert. Il a été ramené au premier plan.",
                ["StatusActivatingSteam"] = "Activation de {0}…",
                ["StatusSteamActivationFailed"] = "Steam est en cours d’exécution, mais sa fenêtre n’a pas pu être amenée au premier plan. Ouvrez-la depuis la zone de notification de Windows.",
                ["AccountsTip"] = "La connexion, Steam Guard et Mémoriser le mot de passe se font directement dans Steam. Si cette copie est déjà ouverte, elle est ramenée au premier plan ; sinon, l’autre copie est fermée avant de l’ouvrir."
            },
            ["de"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["WelcomeDoesNot4"] = "Hält nicht zwei Steam-Sitzungen gleichzeitig geöffnet.",
                ["RememberActive"] = "Passwort merken erkannt",
                ["RememberInactive"] = "Passwort merken nicht erkannt",
                ["RememberNone"] = "Kein Konto erkannt",
                ["PairReadyDetail"] = "Passwort merken erkannt",
                ["StatusSteamActivated"] = "Steam war bereits geöffnet. Es wurde in den Vordergrund geholt.",
                ["StatusActivatingSteam"] = "{0} wird aktiviert…",
                ["StatusSteamActivationFailed"] = "Steam läuft, aber das Fenster konnte nicht in den Vordergrund geholt werden. Öffnen Sie es über den Windows-Infobereich.",
                ["AccountsTip"] = "Anmeldung, Steam Guard und Passwort merken werden direkt in Steam abgeschlossen. Ist diese Kopie bereits geöffnet, wird sie in den Vordergrund geholt; andernfalls wird die andere Kopie vor dem Öffnen geschlossen."
            }
        };
}

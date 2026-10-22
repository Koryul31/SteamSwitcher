using System.ComponentModel;
using System.Globalization;

namespace SteamSwitcher.Setup.Localization;

public sealed record SupportedLanguage(string Code, string NativeName);

public sealed class LocalizationService : INotifyPropertyChanged
{
    private readonly string _systemCode;
    private readonly string _systemNativeName;

    private LocalizationService()
    {
        _systemCode = ResolveSystemCode(CultureInfo.CurrentUICulture);
        _systemNativeName = GetNativeName(_systemCode);
        Apply("System");
    }

    public static IReadOnlyList<SupportedLanguage> SupportedLanguages { get; } =
    [
        new("es", "Español"),
        new("en", "English"),
        new("zh", "简体中文"),
        new("hi", "हिन्दी"),
        new("ar", "العربية"),
        new("pt", "Português"),
        new("ru", "Русский"),
        new("ja", "日本語"),
        new("fr", "Français"),
        new("de", "Deutsch")
    ];

    public static LocalizationService Instance { get; } = new();

    public static event Action? LanguageChanged;

    public string Setting { get; private set; } = "System";
    public string CurrentCode { get; private set; } = "es";
    public string CurrentNativeName => GetNativeName(CurrentCode);
    public string SystemNativeName => _systemNativeName;
    public string SystemCode => _systemCode;
    public bool IsRightToLeft => CurrentCode == "ar";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key] => Get(key);

    public static string T(string key) => Instance[key];

    public static string T(string key, params object[] arguments) =>
        string.Format(CultureInfo.CurrentUICulture, Instance[key], arguments);

    public void Apply(string? setting)
    {
        var normalized = string.IsNullOrWhiteSpace(setting) ? "System" : setting.Trim();
        Setting = normalized.Equals("System", StringComparison.OrdinalIgnoreCase)
            ? "System"
            : NormalizeCode(normalized);

        CurrentCode = Setting == "System" ? _systemCode : Setting;

        try
        {
            var culture = CultureInfo.GetCultureInfo(CurrentCode);
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }
        catch (CultureNotFoundException)
        {
            CurrentCode = "en";
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCode)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentNativeName)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Setting)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRightToLeft)));
        LanguageChanged?.Invoke();
    }

    public static string NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "es";
        }

        var value = code.Trim().Replace('_', '-');
        if (value.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return "zh";
        }

        var twoLetter = value.Length >= 2 ? value[..2].ToLowerInvariant() : value.ToLowerInvariant();
        return SupportedLanguages.Any(language => language.Code == twoLetter) ? twoLetter : "en";
    }

    public static string GetNativeName(string code) =>
        SupportedLanguages.FirstOrDefault(language => language.Code == code)?.NativeName ?? "English";

    public static string ResolveSystemCode(CultureInfo culture)
    {
        var code = NormalizeCode(culture.Name);
        return SupportedLanguages.Any(language => language.Code == code) ? code : "en";
    }

    public string GetLanguageLabel(string setting)
    {
        if (setting.Equals("System", StringComparison.OrdinalIgnoreCase))
        {
            return string.Format(CultureInfo.CurrentCulture, T("LanguageSystemNamed"), _systemNativeName);
        }

        return GetNativeName(NormalizeCode(setting));
    }

    private string Get(string key)
    {
        if (FlowCatalog.Languages.TryGetValue(CurrentCode, out var flow) &&
            flow.TryGetValue(key, out var flowValue))
        {
            return flowValue;
        }

        if (FlowOverrides.Languages.TryGetValue(CurrentCode, out var flowOverrides) &&
            flowOverrides.TryGetValue(key, out var flowOverrideValue))
        {
            return flowOverrideValue;
        }

        if (InstallWizardCatalog.Languages.TryGetValue(CurrentCode, out var installWizard) &&
            installWizard.TryGetValue(key, out var installWizardValue))
        {
            return installWizardValue;
        }

        if (GeneratedCatalog.Languages.TryGetValue(CurrentCode, out var generated) &&
            generated.TryGetValue(key, out var generatedValue))
        {
            return generatedValue;
        }

        if (Catalog.TryGetValue(CurrentCode, out var language) && language.TryGetValue(key, out var value))
        {
            return value;
        }

        if (Catalog.TryGetValue("en", out var english) && english.TryGetValue(key, out value))
        {
            return value;
        }

        if (Catalog.TryGetValue("es", out var spanish) && spanish.TryGetValue(key, out value))
        {
            return value;
        }

        return key;
    }

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Catalog =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["es"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["AppConfig"] = "Configuración",
                ["Minimize"] = "Minimizar",
                ["CloseWindow"] = "Cerrar",
                ["StepInstallations"] = "Instalaciones",
                ["StepAccounts"] = "Cuentas",
                ["StepAccess"] = "Accesos",
                ["StepOptions"] = "Opciones",
                ["StepSummary"] = "Resumen",
                ["StatePending"] = "Pendiente",
                ["StateOptional"] = "Opcional",
                ["StateReview"] = "Revisar",
                ["StateReady"] = "Listo",
                ["NoChanges"] = "Sin cambios pendientes",
                ["UnsavedChanges"] = "Cambios sin guardar",
                ["ConfigUnsaved"] = "Configuración sin guardar",
                ["NoShortcuts"] = "Sin accesos",
                ["PathNotSelected"] = "Sin ruta seleccionada",
                ["ActionBack"] = "Atrás",
                ["ActionSave"] = "Guardar cambios",
                ["ActionContinue"] = "Continuar",
                ["ActionClose"] = "Cerrar",
                ["PairReadyTitle"] = "2 cuentas listas",
                ["PairReadyDetail"] = "Recordar contraseña activo",
                ["PairProgressTitle"] = "Configuración en curso",
                ["PairProgressDetail"] = "Completa las cuentas para continuar",
                ["PageInstallKicker"] = "CONFIGURACIÓN DE INSTALACIONES",
                ["PageInstallSubtitle"] = "Detecta o instala las dos copias de Steam.",
                ["PageAccountsKicker"] = "CONFIGURACIÓN DE CUENTAS",
                ["PageAccountsSubtitle"] = "Inicia sesión una vez en cada cuenta.",
                ["PageAccessKicker"] = "CONFIGURACIÓN DE ACCESOS",
                ["PageAccessSubtitle"] = "Elige nombres, modo y dónde crear los accesos.",
                ["PageOptionsKicker"] = "CONFIGURACIÓN DE OPCIONES",
                ["PageOptionsSubtitle"] = "Ajustes de comportamiento y mantenimiento.",
                ["PageSummaryKicker"] = "RESUMEN DE CONFIGURACIÓN",
                ["PageSummarySubtitle"] = "Revisa la configuración final.",
                ["ChooseFolder"] = "Elegir carpeta",
                ["InstallSteam"] = "Instalar Steam",
                ["OpenFolder"] = "Abrir carpeta",
                ["OpenLogs"] = "Abrir logs",
                ["Reanalyze"] = "Reanalizar",
                ["Swap"] = "Intercambiar A/B",
                ["Check"] = "Comprobar",
                ["CheckAll"] = "Comprobar cuentas",
                ["AccountAssociated"] = "Cuenta asociada",
                ["RememberPassword"] = "Recordar contraseña",
                ["RememberActive"] = "Recordar contraseña activo",
                ["RememberInactive"] = "Recordar contraseña desactivado",
                ["RememberNone"] = "Sin cuenta seleccionada",
                ["AccountsNone"] = "Sin cuentas detectadas",
                ["AccountsOne"] = "1 cuenta detectada",
                ["AccountsMany"] = "{0} cuentas detectadas",
                ["BayNotInstalled"] = "Sin instalar",
                ["BayNeedLogin"] = "Falta iniciar sesión",
                ["BayCheckRemember"] = "Revisar Recordar contraseña",
                ["BayNoConfig"] = "Sin configurar",
                ["BayReady"] = "Listo",
                ["OpenSteam"] = "Abrir Steam",
                ["AccessWhere"] = "Dónde crear los accesos",
                ["AccessDesktop"] = "Escritorio",
                ["AccessStartMenu"] = "Menú Inicio",
                ["AccessOrganization"] = "Organización",
                ["AccessFinalLocation"] = "Ubicación final",
                ["AccessName"] = "Nombre del acceso",
                ["AccessType"] = "Tipo de acceso",
                ["AccessApplyBoth"] = "Aplicar modo a ambas",
                ["AccessTip"] = "Los accesos se crean al guardar. Aplicar modo a ambas copia el tipo de acceso, no el nombre.",
                ["LayoutFolder"] = "En carpeta SteamSwitcher",
                ["LayoutDirect"] = "Directamente en Escritorio y Menú Inicio",
                ["ShortcutNone"] = "No crear",
                ["ShortcutNormal"] = "Normal",
                ["ShortcutBigPicture"] = "Big Picture",
                ["ShortcutBoth"] = "Ambos",
                ["LocationNone"] = "No se crearán accesos directos.",
                ["LocationDesktopFolder"] = "Escritorio\\SteamSwitcher",
                ["LocationDesktopDirect"] = "Escritorio",
                ["LocationStartFolder"] = "Menú Inicio\\Programas\\SteamSwitcher",
                ["LocationStartDirect"] = "Menú Inicio\\Programas",
                ["InstallTip"] = "El instalador oficial de Valve se descarga al instalar. La instalación puede pedir UAC.",
                ["AccountsTip"] = "El login, Steam Guard y Recordar contraseña se completan directamente en Steam. Al abrir una bahía se cierra cualquier Steam que estuviera abierto.",
                ["ThemeTitle"] = "Tema de la interfaz",
                ["ThemeDesc"] = "Sistema usa el tema claro u oscuro configurado en Windows.",
                ["ThemeSystem"] = "Sistema",
                ["ThemeLight"] = "Claro",
                ["ThemeDark"] = "Oscuro",
                ["LanguageTitle"] = "Idioma",
                ["LanguageDesc"] = "El idioma del sistema se usa por defecto; puedes cambiarlo aquí.",
                ["LanguageSystem"] = "Sistema",
                ["LanguageSystemNamed"] = "Sistema ({0})",
                ["BehaviorTitle"] = "Comportamiento",
                ["ConfirmSwitch"] = "Preguntar antes de cerrar el otro Steam",
                ["TimeoutLabel"] = "Tiempo máximo de espera (s)",
                ["BackendTitle"] = "Backend",
                ["BackendConnected"] = "Backend conectado",
                ["BackendMissing"] = "Backend no encontrado",
                ["OptionsTip"] = "Los nombres y la ubicación de los accesos se configuran en la etapa Accesos. Los cambios se guardan con Guardar cambios.",
                ["SummaryAccesses"] = "Accesos",
                ["SummaryBackend"] = "Backend",
                ["OpenAccess"] = "Abrir accesos",
                ["SummaryTip"] = "No necesitas abrir este configurador para jugar. Usa los accesos del Escritorio o del Menú Inicio.",
                ["SummaryReadyTitle"] = "Todo listo",
                ["SummaryReviewTitle"] = "Revisar",
                ["WarningReady"] = "Todo listo. Puedes cerrar el configurador y usar los accesos del Escritorio o del Menú Inicio.",
                ["WarningNoConfig"] = "Aún no has guardado la configuración.",
                ["WarningMissingInstall"] = "Falta una instalación de Steam.",
                ["WarningMissingLogin"] = "Falta iniciar sesión en una cuenta.",
                ["WarningSameAccount"] = "Las dos bahías tienen la misma cuenta.",
                ["WarningRemember"] = "Activa Recordar contraseña en las dos cuentas.",
                ["WarningNoBackend"] = "No se encontró SteamSwitcher.exe.",
                ["WarningMissingShortcuts"] = "Faltan crear o actualizar los accesos.",
                ["WarningPrepared"] = "Las dos bahías están listas para crear los accesos.",
                ["StatusLoaded"] = "Configuración cargada.",
                ["StatusInitial"] = "Revisa los pasos pendientes y guarda cuando termines.",
                ["StatusReady"] = "La configuración está lista. Puedes revisar cualquier paso.",
                ["StatusAccountsUpdated"] = "Estado de cuentas actualizado.",
                ["StatusDetectionUpdated"] = "Detección actualizada.",
                ["StatusNoAccessFolder"] = "No hay una ubicación de accesos activa.",
                ["StatusAlreadyInstalled"] = "{0} ya tiene Steam instalado.",
                ["StatusPreparingInstall"] = "Preparando la instalación de {0}.",
                ["StatusInstallDone"] = "{0} instalado. Continúa con las cuentas.",
                ["StatusSelectValidSteam"] = "Primero elige una instalación de Steam válida.",
                ["StatusClosingSteam"] = "Cerrando cualquier Steam abierto y preparando {0}…",
                ["StatusModeApplied"] = "Modo de accesos de {0} aplicado a {1}.",
                ["StatusSwapped"] = "Bahías A y B intercambiadas.",
                ["StatusLanguageChanged"] = "Idioma actualizado.",
                ["StatusSaved"] = "Cambios aplicados.",
                ["StatusSavedRemember"] = "Cambios aplicados. Atención: falta activar Recordar contraseña.",
                ["StatusSavedShortcutsFailed"] = "La configuración se guardó, pero los accesos fallaron: {0}",
                ["StatusSavedFailed"] = "No se pudieron aplicar los cambios: {0}",
                ["StatusSteamOpenSuccess"] = "Steam abierto. Inicia sesión y activa Recordar contraseña.",
                ["StatusSteamCloseFailed"] = "Steam no pudo cerrarse. No se abrió otra instalación para evitar abrir la incorrecta.",
                ["StatusSteamNotFound"] = "No se encontró steam.exe en esta instalación.",
                ["StatusSteamOpenFailed"] = "No se pudo abrir Steam: {0}",
                ["StatusBrowseTitle"] = "Elegir carpeta para {0}",
                ["StatusValveChooseFolder"] = "Elige una carpeta de instalación.",
                ["StatusValveAlreadyInstalled"] = "Steam ya está instalado en esa carpeta.",
                ["StatusValveDownloadFailed"] = "No se pudo descargar SteamSetup.exe desde Valve.",
                ["StatusValveSignatureInvalid"] = "La firma de SteamSetup.exe no es válida o no pertenece a Valve.",
                ["StatusValveStartFailed"] = "No se pudo iniciar SteamSetup.exe.",
                ["StatusValveExitCode"] = "SteamSetup terminó con código {0}.",
                ["StatusValveInstalled"] = "Steam se instaló correctamente.",
                ["StatusValveCancelled"] = "La instalación fue cancelada.",
                ["StatusValveDownloading"] = "Descargando el instalador oficial de Valve…",
                ["StatusValveVerifying"] = "Verificando la firma digital de Valve…",
                ["StatusValveOpeningUac"] = "Abriendo el instalador. Aprueba el aviso de UAC…",
                ["StatusValveInstalling"] = "Instalando Steam en la carpeta elegida…",
                ["StatusValveDownloadPercent"] = "Descargando SteamSetup… {0}%",
                ["StatusValveServerFailed"] = "No se pudo descargar desde un servidor: {0}",
                ["StatusValveMissingExe"] = "El instalador terminó, pero no se encontró steam.exe en la carpeta elegida. Prueba otra ruta.",
                ["StatusShortcutBackendMissing"] = "No se encontró SteamSwitcher.exe.",
                ["StatusShortcutComUnavailable"] = "WScript.Shell no está disponible.",
                ["StatusShortcutStartFailed"] = "No se pudo iniciar WScript.Shell.",
                ["ValidationNeedInstalls"] = "Instala o selecciona las dos copias de Steam antes de guardar.",
                ["ValidationSameDirectory"] = "Las dos bahías deben usar carpetas diferentes.",
                ["ValidationNeedAccounts"] = "Selecciona la cuenta de cada instalación.",
                ["ValidationSameAccount"] = "Las dos bahías no pueden usar la misma cuenta.",
                ["ValidationNoBackend"] = "No se encontró SteamSwitcher.exe. El configurador necesita el backend para crear accesos.",
                ["ValidationTimeout"] = "El tiempo de espera debe estar entre 5 y 120 segundos.",
                ["DialogTitle"] = "SteamSwitcher",
                ["DialogUnsavedContinue"] = "Hay cambios sin guardar. Guarda los cambios antes de continuar.",
                ["DialogUnsavedClose"] = "Hay cambios sin guardar. ¿Quieres guardarlos antes de cerrar?",
                ["ShortcutSetupDescription"] = "Abrir el configurador de SteamSwitcher",
                ["ShortcutNormalDescription"] = "Abrir {0} en Steam",
                ["ShortcutBigDescription"] = "Abrir {0} en Steam Big Picture",
                ["WelcomeKicker"] = "BIENVENIDA",
                ["WelcomeTitle"] = "Dos Steam. Un acceso para cada una.",
                ["WelcomeSubtitle"] = "SteamSwitcher prepara dos copias independientes de Steam y deja cada cuenta a un clic. Elige el acceso de la cuenta que quieras; el configurador se encarga del resto.",
                ["WelcomeContinue"] = "Continuar",
                ["WelcomeSkip"] = "Saltar introducción",
                ["WelcomeStart"] = "Comenzar",
                ["WelcomeHint"] = "Podrás cambiar todo después desde Opciones.",
                ["WelcomeSceneTwoTitle"] = "Así funciona SteamSwitcher.",
                ["WelcomeSceneTwoSubtitle"] = "Lo esencial antes de empezar.",
                ["WelcomeDoesTitle"] = "Qué hace",
                ["WelcomeDoes1"] = "Detecta tus dos copias de Steam o ayuda a instalar la que falte.",
                ["WelcomeDoes2"] = "Te guía para iniciar sesión una vez en cada cuenta.",
                ["WelcomeDoes3"] = "Crea accesos Normal y Big Picture en Escritorio o Menú Inicio.",
                ["WelcomeDoes4"] = "Cierra el Steam abierto antes de abrir la cuenta elegida.",
                ["WelcomeDoesNotTitle"] = "Qué no hace",
                ["WelcomeDoesNot1"] = "No guarda tu contraseña ni tus credenciales.",
                ["WelcomeDoesNot2"] = "No evita Steam Guard ni la verificación en dos pasos.",
                ["WelcomeDoesNot3"] = "No modifica juegos, partidas ni archivos de Steam.",
                ["WelcomeDoesNot4"] = "No cambia de cuenta con Steam abierto ni queda residente en segundo plano.",
                ["WelcomeDiagramCaption"] = "Tú eliges la cuenta. El acceso hace el resto.",
                ["WelcomeAfterSetup"] = "No necesitas abrir este configurador para jugar. Usa los accesos del Escritorio o del Menú Inicio.",
                ["WelcomeReplayTitle"] = "Introducción",
                ["WelcomeReplayDesc"] = "Vuelve a ver la presentación inicial de SteamSwitcher.",
                ["WelcomeReplayAction"] = "Ver introducción",
                ["VersionText"] = "Configurador 1.3.2 · Backend 1.3.2"
            },
            ["en"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["AppConfig"] = "Configuration",
                ["Minimize"] = "Minimize",
                ["CloseWindow"] = "Close",
                ["StepInstallations"] = "Installations",
                ["StepAccounts"] = "Accounts",
                ["StepAccess"] = "Shortcuts",
                ["StepOptions"] = "Options",
                ["StepSummary"] = "Summary",
                ["StatePending"] = "Pending",
                ["StateOptional"] = "Optional",
                ["StateReview"] = "Review",
                ["StateReady"] = "Ready",
                ["NoChanges"] = "No pending changes",
                ["UnsavedChanges"] = "Unsaved changes",
                ["ConfigUnsaved"] = "Configuration not saved",
                ["NoShortcuts"] = "No shortcuts",
                ["PathNotSelected"] = "No path selected",
                ["ActionBack"] = "Back",
                ["ActionSave"] = "Save changes",
                ["ActionContinue"] = "Continue",
                ["ActionClose"] = "Close",
                ["PairReadyTitle"] = "2 accounts ready",
                ["PairReadyDetail"] = "Remember password enabled",
                ["PairProgressTitle"] = "Setup in progress",
                ["PairProgressDetail"] = "Finish the accounts to continue",
                ["PageInstallKicker"] = "INSTALLATION SETUP",
                ["PageInstallSubtitle"] = "Detect or install both Steam copies.",
                ["PageAccountsKicker"] = "ACCOUNT SETUP",
                ["PageAccountsSubtitle"] = "Sign in once for each account.",
                ["PageAccessKicker"] = "SHORTCUT SETUP",
                ["PageAccessSubtitle"] = "Choose names, mode, and where to create shortcuts.",
                ["PageOptionsKicker"] = "OPTIONS",
                ["PageOptionsSubtitle"] = "Behavior and maintenance settings.",
                ["PageSummaryKicker"] = "SETUP SUMMARY",
                ["PageSummarySubtitle"] = "Review the final configuration.",
                ["ChooseFolder"] = "Choose folder",
                ["InstallSteam"] = "Install Steam",
                ["OpenFolder"] = "Open folder",
                ["OpenLogs"] = "Open logs",
                ["Reanalyze"] = "Scan again",
                ["Swap"] = "Swap A/B",
                ["Check"] = "Check",
                ["CheckAll"] = "Check accounts",
                ["AccountAssociated"] = "Linked account",
                ["RememberPassword"] = "Remember password",
                ["RememberActive"] = "Remember password enabled",
                ["RememberInactive"] = "Remember password disabled",
                ["RememberNone"] = "No account selected",
                ["AccountsNone"] = "No accounts detected",
                ["AccountsOne"] = "1 account detected",
                ["AccountsMany"] = "{0} accounts detected",
                ["BayNotInstalled"] = "Not installed",
                ["BayNeedLogin"] = "Sign-in required",
                ["BayCheckRemember"] = "Check Remember password",
                ["BayNoConfig"] = "Not configured",
                ["BayReady"] = "Ready",
                ["OpenSteam"] = "Open Steam",
                ["AccessWhere"] = "Where to create shortcuts",
                ["AccessDesktop"] = "Desktop",
                ["AccessStartMenu"] = "Start Menu",
                ["AccessOrganization"] = "Organization",
                ["AccessFinalLocation"] = "Final location",
                ["AccessName"] = "Shortcut name",
                ["AccessType"] = "Shortcut type",
                ["AccessApplyBoth"] = "Apply mode to both",
                ["AccessTip"] = "Shortcuts are created when you save. Apply mode to both copies only the shortcut type, not the name.",
                ["LayoutFolder"] = "In SteamSwitcher folder",
                ["LayoutDirect"] = "Directly on Desktop and Start Menu",
                ["ShortcutNone"] = "Do not create",
                ["ShortcutNormal"] = "Normal",
                ["ShortcutBigPicture"] = "Big Picture",
                ["ShortcutBoth"] = "Both",
                ["LocationNone"] = "No shortcuts will be created.",
                ["LocationDesktopFolder"] = "Desktop\\SteamSwitcher",
                ["LocationDesktopDirect"] = "Desktop",
                ["LocationStartFolder"] = "Start Menu\\Programs\\SteamSwitcher",
                ["LocationStartDirect"] = "Start Menu\\Programs",
                ["InstallTip"] = "The official Valve installer is downloaded during installation. Windows may ask for UAC.",
                ["AccountsTip"] = "Login, Steam Guard and Remember password are completed directly in Steam. Opening a bay closes any Steam that was already open.",
                ["ThemeTitle"] = "Interface theme",
                ["ThemeDesc"] = "System follows the light or dark theme configured in Windows.",
                ["ThemeSystem"] = "System",
                ["ThemeLight"] = "Light",
                ["ThemeDark"] = "Dark",
                ["LanguageTitle"] = "Language",
                ["LanguageDesc"] = "The Windows language is used by default; you can change it here.",
                ["LanguageSystem"] = "System",
                ["LanguageSystemNamed"] = "System ({0})",
                ["BehaviorTitle"] = "Behavior",
                ["ConfirmSwitch"] = "Ask before closing the other Steam",
                ["TimeoutLabel"] = "Maximum wait time (s)",
                ["BackendTitle"] = "Backend",
                ["BackendConnected"] = "Backend connected",
                ["BackendMissing"] = "Backend not found",
                ["OptionsTip"] = "Shortcut names and locations are configured in the Shortcuts step. Changes on this screen are saved with Save changes.",
                ["SummaryAccesses"] = "Shortcuts",
                ["SummaryBackend"] = "Backend",
                ["OpenAccess"] = "Open shortcuts",
                ["SummaryTip"] = "You do not need to open this setup to play. Use the Desktop or Start Menu shortcuts.",
                ["SummaryReadyTitle"] = "All ready",
                ["SummaryReviewTitle"] = "Review",
                ["WarningReady"] = "All ready. You can close the setup and use the Desktop or Start Menu shortcuts.",
                ["WarningNoConfig"] = "You have not saved the configuration yet.",
                ["WarningMissingInstall"] = "One Steam installation is missing.",
                ["WarningMissingLogin"] = "You need to sign in to one account.",
                ["WarningSameAccount"] = "Both bays use the same account.",
                ["WarningRemember"] = "Enable Remember password on both accounts.",
                ["WarningNoBackend"] = "SteamSwitcher.exe was not found.",
                ["WarningMissingShortcuts"] = "Shortcuts need to be created or updated.",
                ["WarningPrepared"] = "Both bays are ready to create shortcuts.",
                ["StatusLoaded"] = "Configuration loaded.",
                ["StatusInitial"] = "Review the pending steps and save when finished.",
                ["StatusReady"] = "Configuration ready. You can review any step.",
                ["StatusAccountsUpdated"] = "Account status updated.",
                ["StatusDetectionUpdated"] = "Detection updated.",
                ["StatusNoAccessFolder"] = "There is no active shortcut location.",
                ["StatusAlreadyInstalled"] = "{0} already has Steam installed.",
                ["StatusPreparingInstall"] = "Preparing the installation of {0}.",
                ["StatusInstallDone"] = "{0} installed. Continue with the accounts.",
                ["StatusSelectValidSteam"] = "Choose a valid Steam installation first.",
                ["StatusClosingSteam"] = "Closing any open Steam and preparing {0}…",
                ["StatusModeApplied"] = "{0} shortcut mode applied to {1}.",
                ["StatusSwapped"] = "Bays A and B swapped.",
                ["StatusLanguageChanged"] = "Language updated.",
                ["StatusSaved"] = "Changes applied.",
                ["StatusSavedRemember"] = "Changes applied. Warning: Remember password is not enabled.",
                ["StatusSavedShortcutsFailed"] = "Configuration was saved, but shortcuts failed: {0}",
                ["StatusSavedFailed"] = "Could not apply changes: {0}",
                ["StatusSteamOpenSuccess"] = "Steam opened. Sign in and enable Remember password.",
                ["StatusSteamCloseFailed"] = "Steam could not be closed. Another installation was not opened to avoid opening the wrong one.",
                ["StatusSteamNotFound"] = "steam.exe was not found in this installation.",
                ["StatusSteamOpenFailed"] = "Could not open Steam: {0}",
                ["StatusBrowseTitle"] = "Choose folder for {0}",
                ["StatusValveChooseFolder"] = "Choose an installation folder.",
                ["StatusValveAlreadyInstalled"] = "Steam is already installed in that folder.",
                ["StatusValveDownloadFailed"] = "Could not download SteamSetup.exe from Valve.",
                ["StatusValveSignatureInvalid"] = "SteamSetup.exe signature is invalid or does not belong to Valve.",
                ["StatusValveStartFailed"] = "Could not start SteamSetup.exe.",
                ["StatusValveExitCode"] = "SteamSetup exited with code {0}.",
                ["StatusValveInstalled"] = "Steam installed successfully.",
                ["StatusValveCancelled"] = "The installation was cancelled.",
                ["StatusValveDownloading"] = "Downloading the official Valve installer…",
                ["StatusValveVerifying"] = "Verifying Valve digital signature…",
                ["StatusValveOpeningUac"] = "Opening the installer. Approve the UAC prompt…",
                ["StatusValveInstalling"] = "Installing Steam in the selected folder…",
                ["StatusValveDownloadPercent"] = "Downloading SteamSetup… {0}%",
                ["StatusValveServerFailed"] = "Could not download from a server: {0}",
                ["StatusValveMissingExe"] = "The installer finished, but steam.exe was not found in the selected folder. Try another path.",
                ["StatusShortcutBackendMissing"] = "SteamSwitcher.exe was not found.",
                ["StatusShortcutComUnavailable"] = "WScript.Shell is not available.",
                ["StatusShortcutStartFailed"] = "Could not start WScript.Shell.",
                ["ValidationNeedInstalls"] = "Install or select both Steam copies before saving.",
                ["ValidationSameDirectory"] = "The two bays must use different folders.",
                ["ValidationNeedAccounts"] = "Select the account for each installation.",
                ["ValidationSameAccount"] = "The two bays cannot use the same account.",
                ["ValidationNoBackend"] = "SteamSwitcher.exe was not found. The setup needs the backend to create shortcuts.",
                ["ValidationTimeout"] = "The wait time must be between 5 and 120 seconds.",
                ["DialogTitle"] = "SteamSwitcher",
                ["DialogUnsavedContinue"] = "There are unsaved changes. Save them before continuing.",
                ["DialogUnsavedClose"] = "There are unsaved changes. Do you want to save them before closing?",
                ["ShortcutSetupDescription"] = "Open the SteamSwitcher setup",
                ["ShortcutNormalDescription"] = "Open {0} in Steam",
                ["ShortcutBigDescription"] = "Open {0} in Steam Big Picture",
                ["WelcomeKicker"] = "WELCOME",
                ["WelcomeTitle"] = "Two Steams. One shortcut for each.",
                ["WelcomeSubtitle"] = "SteamSwitcher prepares two independent Steam copies and puts each account one click away. Choose the shortcut for the account you want; the setup handles the rest.",
                ["WelcomeContinue"] = "Continue",
                ["WelcomeSkip"] = "Skip introduction",
                ["WelcomeStart"] = "Start",
                ["WelcomeHint"] = "You can change everything later in Options.",
                ["WelcomeSceneTwoTitle"] = "How SteamSwitcher works.",
                ["WelcomeSceneTwoSubtitle"] = "The essentials before you start.",
                ["WelcomeDoesTitle"] = "What it does",
                ["WelcomeDoes1"] = "Detects both Steam copies or helps install the missing one.",
                ["WelcomeDoes2"] = "Guides you through signing in once to each account.",
                ["WelcomeDoes3"] = "Creates Normal and Big Picture shortcuts on the Desktop or Start Menu.",
                ["WelcomeDoes4"] = "Closes open Steam before launching the selected account.",
                ["WelcomeDoesNotTitle"] = "What it does not do",
                ["WelcomeDoesNot1"] = "Does not store your password or credentials.",
                ["WelcomeDoesNot2"] = "Does not bypass Steam Guard or two-step verification.",
                ["WelcomeDoesNot3"] = "Does not modify games, saves, or Steam files.",
                ["WelcomeDoesNot4"] = "Does not switch accounts while Steam is open or stay running in the background.",
                ["WelcomeDiagramCaption"] = "You choose the account. The shortcut does the rest.",
                ["WelcomeAfterSetup"] = "You do not need to open this setup to play. Use the Desktop or Start Menu shortcuts.",
                ["WelcomeReplayTitle"] = "Introduction",
                ["WelcomeReplayDesc"] = "Watch the SteamSwitcher introduction again.",
                ["WelcomeReplayAction"] = "View introduction",
                ["VersionText"] = "Setup 1.3.2 · Backend 1.3.2"
            },
            ["zh"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["AppConfig"] = "配置",
                ["Minimize"] = "最小化",
                ["CloseWindow"] = "关闭",
                ["StepInstallations"] = "安装",
                ["StepAccounts"] = "账户",
                ["StepAccess"] = "快捷方式",
                ["StepOptions"] = "选项",
                ["StepSummary"] = "摘要",
                ["StatePending"] = "待处理",
                ["StateOptional"] = "可选",
                ["StateReview"] = "检查",
                ["StateReady"] = "就绪",
                ["NoChanges"] = "没有待保存的更改",
                ["UnsavedChanges"] = "未保存的更改",
                ["ConfigUnsaved"] = "配置未保存",
                ["ActionBack"] = "返回",
                ["ActionSave"] = "保存更改",
                ["ActionContinue"] = "继续",
                ["ActionClose"] = "关闭",
                ["PairReadyTitle"] = "2 个账户已就绪",
                ["PairReadyDetail"] = "已启用记住密码",
                ["PairProgressTitle"] = "配置进行中",
                ["PairProgressDetail"] = "完成账户设置以继续",
                ["PageInstallKicker"] = "安装设置",
                ["PageInstallSubtitle"] = "检测或安装两个 Steam 副本。",
                ["PageAccountsKicker"] = "账户设置",
                ["PageAccountsSubtitle"] = "分别登录每个账户。",
                ["PageAccessKicker"] = "快捷方式设置",
                ["PageAccessSubtitle"] = "选择名称、模式和快捷方式位置。",
                ["PageOptionsKicker"] = "选项",
                ["PageOptionsSubtitle"] = "行为和維護设置。",
                ["PageSummaryKicker"] = "配置摘要",
                ["PageSummarySubtitle"] = "检查最终配置。",
                ["ChooseFolder"] = "选择文件夹",
                ["InstallSteam"] = "安装 Steam",
                ["OpenFolder"] = "打开文件夹",
                ["OpenLogs"] = "打开日志",
                ["Reanalyze"] = "重新扫描",
                ["Swap"] = "交换 A/B",
                ["Check"] = "检查",
                ["CheckAll"] = "检查账户",
                ["AccountAssociated"] = "关联账户",
                ["RememberPassword"] = "记住密码",
                ["RememberActive"] = "已启用记住密码",
                ["RememberInactive"] = "已禁用记住密码",
                ["RememberNone"] = "未选择账户",
                ["AccountsNone"] = "未检测到账户",
                ["AccountsOne"] = "检测到 1 个账户",
                ["AccountsMany"] = "检测到 {0} 个账户",
                ["BayNotInstalled"] = "未安装",
                ["BayNeedLogin"] = "需要登录",
                ["BayCheckRemember"] = "检查记住密码",
                ["BayNoConfig"] = "未配置",
                ["BayReady"] = "就绪",
                ["OpenSteam"] = "打开 Steam",
                ["AccessWhere"] = "在何处创建快捷方式",
                ["AccessDesktop"] = "桌面",
                ["AccessStartMenu"] = "开始菜单",
                ["AccessOrganization"] = "组织方式",
                ["AccessFinalLocation"] = "最终位置",
                ["AccessName"] = "快捷方式名称",
                ["AccessType"] = "快捷方式类型",
                ["AccessApplyBoth"] = "应用到两个副本",
                ["AccessTip"] = "保存后会创建快捷方式。应用到两个副本只复制类型，不复制名称。",
                ["LayoutFolder"] = "在 SteamSwitcher 文件夹中",
                ["LayoutDirect"] = "直接放在桌面和开始菜单",
                ["ShortcutNone"] = "不创建",
                ["ShortcutNormal"] = "普通",
                ["ShortcutBigPicture"] = "Big Picture",
                ["ShortcutBoth"] = "两者",
                ["LocationNone"] = "不会创建快捷方式。",
                ["LocationDesktopFolder"] = "桌面\\SteamSwitcher",
                ["LocationDesktopDirect"] = "桌面",
                ["LocationStartFolder"] = "开始菜单\\程序\\SteamSwitcher",
                ["LocationStartDirect"] = "开始菜单\\程序",
                ["InstallTip"] = "安装时会下载 Valve 官方安装程序，可能需要 UAC 权限。",
                ["AccountsTip"] = "登录、Steam Guard 和记住密码都在 Steam 中完成。",
                ["ThemeTitle"] = "界面主题",
                ["ThemeDesc"] = "系统会使用 Windows 的浅色或深色主题。",
                ["ThemeSystem"] = "系统",
                ["ThemeLight"] = "浅色",
                ["ThemeDark"] = "深色",
                ["LanguageTitle"] = "语言",
                ["LanguageDesc"] = "默认使用 Windows 语言，可在此更改。",
                ["LanguageSystem"] = "系统",
                ["LanguageSystemNamed"] = "系统 ({0})",
                ["BehaviorTitle"] = "行为",
                ["ConfirmSwitch"] = "关闭另一个 Steam 前询问",
                ["TimeoutLabel"] = "最长等待时间（秒）",
                ["BackendTitle"] = "后端",
                ["BackendConnected"] = "后端已连接",
                ["BackendMissing"] = "未找到后端",
                ["OptionsTip"] = "快捷方式名称和位置在“快捷方式”步骤中设置。此页面的更改通过“保存更改”保存。",
                ["SummaryAccesses"] = "快捷方式",
                ["SummaryBackend"] = "后端",
                ["OpenAccess"] = "打开快捷方式",
                ["SummaryTip"] = "玩游戏时不需要打开此配置器，请使用桌面或开始菜单快捷方式。",
                ["SummaryReadyTitle"] = "全部就绪",
                ["SummaryReviewTitle"] = "检查",
                ["WarningReady"] = "全部就绪。可以关闭配置器并使用桌面或开始菜单快捷方式。",
                ["WarningNoConfig"] = "尚未保存配置。",
                ["WarningMissingInstall"] = "缺少一个 Steam 安装。",
                ["WarningMissingLogin"] = "需要登录一个账户。",
                ["WarningSameAccount"] = "两个位置使用同一账户。",
                ["WarningRemember"] = "请在两个账户中启用记住密码。",
                ["WarningNoBackend"] = "未找到 SteamSwitcher.exe。",
                ["WarningMissingShortcuts"] = "快捷方式需要创建或更新。",
                ["WarningPrepared"] = "两个位置已准备好创建快捷方式。",
                ["StatusLoaded"] = "配置已载入。",
                ["StatusInitial"] = "检查待处理步骤并在完成后保存。",
                ["StatusReady"] = "配置已就绪，可以检查任一步骤。",
                ["StatusAccountsUpdated"] = "账户状态已更新。",
                ["StatusDetectionUpdated"] = "检测已更新。",
                ["StatusNoAccessFolder"] = "没有可用的快捷方式位置。",
                ["StatusAlreadyInstalled"] = "{0} 已安装 Steam。",
                ["StatusPreparingInstall"] = "正在准备安装 {0}。",
                ["StatusInstallDone"] = "{0} 已安装，继续设置账户。",
                ["StatusSelectValidSteam"] = "请先选择有效的 Steam 安装。",
                ["StatusClosingSteam"] = "正在关闭已打开的 Steam 并准备 {0}…",
                ["StatusModeApplied"] = "已将 {0} 的快捷方式模式应用到 {1}。",
                ["StatusSwapped"] = "已交换 A 和 B。",
                ["StatusLanguageChanged"] = "语言已更新。",
                ["StatusSaved"] = "更改已应用。",
                ["StatusSavedRemember"] = "更改已应用。注意：尚未启用记住密码。",
                ["StatusSavedShortcutsFailed"] = "配置已保存，但快捷方式创建失败：{0}",
                ["StatusSavedFailed"] = "无法应用更改：{0}",
                ["StatusSteamOpenSuccess"] = "Steam 已打开，请登录并启用记住密码。",
                ["StatusSteamCloseFailed"] = "无法关闭 Steam，为避免打开错误的安装，未打开另一个副本。",
                ["StatusSteamNotFound"] = "此安装中未找到 steam.exe。",
                ["StatusSteamOpenFailed"] = "无法打开 Steam：{0}",
                ["StatusBrowseTitle"] = "为 {0} 选择文件夹",
                ["StatusValveChooseFolder"] = "请选择安装文件夹。",
                ["StatusValveAlreadyInstalled"] = "该文件夹已安装 Steam。",
                ["StatusValveDownloadFailed"] = "无法从 Valve 下载 SteamSetup.exe。",
                ["StatusValveSignatureInvalid"] = "SteamSetup.exe 的签名无效或不属于 Valve。",
                ["StatusValveStartFailed"] = "无法启动 SteamSetup.exe。",
                ["StatusValveExitCode"] = "SteamSetup 退出代码 {0}。",
                ["StatusValveInstalled"] = "Steam 安装成功。",
                ["StatusValveCancelled"] = "安装已取消。",
                ["StatusShortcutBackendMissing"] = "未找到 SteamSwitcher.exe。",
                ["StatusShortcutComUnavailable"] = "WScript.Shell 不可用。",
                ["StatusShortcutStartFailed"] = "无法启动 WScript.Shell。",
                ["ValidationNeedInstalls"] = "保存前请安装或选择两个 Steam 副本。",
                ["ValidationSameDirectory"] = "两个位置必须使用不同文件夹。",
                ["ValidationNeedAccounts"] = "请为每个安装选择账户。",
                ["ValidationSameAccount"] = "两个位置不能使用同一账户。",
                ["ValidationNoBackend"] = "未找到 SteamSwitcher.exe。配置器需要后端来创建快捷方式。",
                ["ValidationTimeout"] = "等待时间必须在 5 到 120 秒之间。",
                ["DialogTitle"] = "SteamSwitcher",
                ["DialogUnsavedContinue"] = "有未保存的更改。请先保存再继续。",
                ["DialogUnsavedClose"] = "有未保存的更改。关闭前要保存吗？",
                ["ShortcutSetupDescription"] = "打开 SteamSwitcher 配置器",
                ["ShortcutNormalDescription"] = "在 Steam 中打开 {0}",
                ["ShortcutBigDescription"] = "在 Steam Big Picture 中打开 {0}",
                ["VersionText"] = "配置器 1.3.2 · 后端 1.3.2"
            }
        };
}

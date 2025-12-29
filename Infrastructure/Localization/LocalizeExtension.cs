using System.Windows.Markup;
using WpfBinding = System.Windows.Data.Binding;
using WpfBindingMode = System.Windows.Data.BindingMode;

namespace InternetMonitor.Infrastructure.Localization;

[MarkupExtensionReturnType(typeof(string))]
public class LocalizeExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public LocalizeExtension() { }

    public LocalizeExtension(string key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrEmpty(Key))
            return string.Empty;

        var binding = new WpfBinding($"[{Key}]")
        {
            Source = LocalizationService.Instance,
            Mode = WpfBindingMode.OneWay
        };

        return binding.ProvideValue(serviceProvider);
    }
}

// Binding converter for localization
public class LocalizationBinding : WpfBinding
{
    public LocalizationBinding(string key) : base($"[{key}]")
    {
        Source = LocalizationService.Instance;
        Mode = WpfBindingMode.OneWay;
    }
}

using Microsoft.Extensions.Options;

namespace GmrProcessor.Extensions;

public static class OptionsExtensions
{
    public static OptionsBuilder<TOptions> AddValidateOptions<TOptions>(
        this IServiceCollection services,
        string section
    )
        where TOptions : class => services.AddOptions<TOptions>().BindConfiguration(section).ValidateDataAnnotations();
}

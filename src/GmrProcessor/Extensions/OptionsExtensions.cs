using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;

namespace GmrProcessor.Extensions;

[ExcludeFromCodeCoverage]
public static class OptionsExtensions
{
    public static OptionsBuilder<TOptions> AddValidateOptions<TOptions>(
        this IServiceCollection services,
        string section
    )
        where TOptions : class => services.AddOptions<TOptions>().BindConfiguration(section).ValidateDataAnnotations();
}

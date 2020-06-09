# i18n.Core

- **stable** ![Nuget](https://img.shields.io/nuget/v/i18n.core) 
- **pre-release**: ![Nuget](https://img.shields.io/nuget/vpre/i18n.core)
- [![Dependabot Status](https://api.dependabot.com/badges/status?host=github&repo=fintermobilityas/i18n.core)](https://dependabot.com)
 
| Build server | Platforms | Build status |
|--------------|----------|--------------|
| Github Actions | windows-latest, ubuntu-latest | Branch: develop ![i18n.core](https://github.com/fintermobilityas/i18n.core/workflows/i18n.core/badge.svg?branch=develop) |
| Github Actions | windows-latest, ubuntu-latest | Branch: master ![i18n.core](https://github.com/fintermobilityas/i18n.core/workflows/i18n.core/badge.svg?branch=master) |

## Smart internationalization for ASP.NET Core

Sponsored by Finter Mobility AS.

### Platforms supported

- ASP.NET Core 3.1 

### Introduction

The i18n library is designed to replace the use of .NET resources in favor 
of an **easier**, globally recognized standard for localizing ASP.NET-based web applications.

### Project configuration (ASP.NET CORE)

```xml
<PackageReference Include="i18n.Core" Version="1.0.10" />
```

```cs
// This method gets called by the runtime. Use this method to add services to the container.
public void ConfigureServices(IServiceCollection services)
{
    services.AddI18NLocalization(HostEnvironment, options =>
    {
        var supportedCultures = new[]
        {
            new CultureInfo("nb-NO"),
            new CultureInfo("en-US")
        };

        var defaultCulture = supportedCultures.Single(x => x.Name == "en-US");

        options.DefaultRequestCulture = new RequestCulture(defaultCulture);
        options.SupportedCultures = supportedCultures;
        options.SupportedUICultures = supportedCultures;
        options.RequestCultureProviders = new List<IRequestCultureProvider>
        {
            new CookieRequestCultureProvider()
        };
    });
}
```

```cs
// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    app.UseI18NRequestLocalization();
}
```

### Automatically update *.pot file (Portable Object Template)

```
dotnet tool install pot -g
pot --watch
```

### Custom configuration (Web.config)

NB! This is not required for this to work as you can configure this middleware by resolving `IOptions<I18NLocalizationOptions>`. It's available for legacy reasons only.

```xml
<?xml version="1.0"?>

<configuration>
  <appSettings>
    <add key="i18n.DirectoriesToScan" value=".;"/>
    <add key="i18n.GenerateTemplatePerFile" value="false"/>
  </appSettings>
</configuration>
```

### Demo

A demo project is available in this repository. You can find it [here](https://github.com/fintermobilityas/i18n.core/tree/master/src/i18n.Demo)

### Special thanks to

This project is mainly built on hard work of the following projects:

- https://github.com/OrchardCMS/OrchardCore
- https://github.com/turquoiseowl/i18n

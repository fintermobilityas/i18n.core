# i18n.Core

## Smart internationalization for ASP.NET Core

Sponsored by Finter Mobility AS.

### Platforms supported

- ASP.NET Core 3.1 

### Introduction

The i18n library is designed to replace the use of .NET resources in favor 
of an **easier**, globally recognized standard for localizing ASP.NET-based web applications.

### Project configuration (ASP.NET CORE)

```xml
<PackageReference Include="i18n.Core" Version="*" />
```

```cs
// This method gets called by the runtime. Use this method to add services to the container.
public void ConfigureServices(IServiceCollection services)
{
    var i18NRootDirectory = Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location)!);
    services.AddI18NLocalization(i18NRootDirectory, options =>
    {
        var supportedCultures = new[]
        {
            new CultureInfo("no"),
            new CultureInfo("en")
        };

        var defaultCulture = supportedCultures.Single(x => x.Name == "en");

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

```ps
pot --watch
```

### Custom configuration (web.config)



<?xml version="1.0"?>

<configuration>
  <appSettings>
    <add key="i18n.DirectoriesToScan" value=".;"/>
    <add key="i18n.GenerateTemplatePerFile" value="false"/>
  </appSettings>
</configuration>

### Special thanks to

This project is mainly built on hard work of the following projects:

- https://github.com/OrchardCMS/OrchardCore
- https://github.com/turquoiseowl/i18n

# Cheat sheet: .NET 10, solutions y ASP.NET Core

## Mapa rápido Java → .NET

| Java / Spring | .NET |
|---|---|
| JDK | .NET SDK |
| JVM | CLR |
| Maven | `dotnet` CLI + MSBuild + NuGet |
| `pom.xml` | `.csproj` |
| proyecto Maven multimódulo | solution `.slnx` / `.sln` |
| JAR | assembly DLL |
| Spring Boot | ASP.NET Core |
| `Application.java` | `Program.cs` |
| `application.yml` | `appsettings.json` |
| `@RestController` | `[ApiController]` + `ControllerBase` |
| `@GetMapping` | `[HttpGet]` |
| `@RequestMapping` | `[Route]` |

## Estructura inicial

```text
dotnet-course/
├── CustomerService.slnx
└── src/
    └── CustomerService.Api/
        ├── Controllers/
        ├── Properties/
        │   └── launchSettings.json
        ├── appsettings.json
        ├── appsettings.Development.json
        ├── CustomerService.Api.csproj
        └── Program.cs
```

## Comandos de diagnóstico

Se pueden ejecutar desde cualquier directorio:

```powershell
dotnet --info
dotnet --list-sdks
dotnet --list-runtimes
git --version
docker --version
docker compose version
```

## Crear la solución y la Web API

Directorio:

```text
dotnet-course/
```

```powershell
# .NET 10 genera .slnx de forma predeterminada
dotnet new sln -n CustomerService

# API .NET 10 basada en Controllers
dotnet new webapi -n CustomerService.Api -o .\src\CustomerService.Api --framework net10.0 --use-controllers

# Incorporar el project a la solution
dotnet sln .\CustomerService.slnx add .\src\CustomerService.Api\CustomerService.Api.csproj

# Listar projects incluidos
dotnet sln .\CustomerService.slnx list
```

Para crear el formato tradicional:

```powershell
dotnet new sln -n CustomerService --format sln
```

## Restaurar, compilar y ejecutar

Directorio:

```text
dotnet-course/
```

```powershell
dotnet restore .\CustomerService.slnx
dotnet build .\CustomerService.slnx
dotnet build .\CustomerService.slnx --no-restore
dotnet run --project .\src\CustomerService.Api\CustomerService.Api.csproj --launch-profile http
```

- `restore`: resuelve paquetes NuGet y genera información en `obj/`.
- `build`: compila y produce el assembly en `bin/Debug/net10.0/`.
- `run`: compila si hace falta e inicia la aplicación.
- `Ctrl+C`: detiene la aplicación.

## Probar endpoints

```powershell
curl.exe http://localhost:PUERTO/weatherforecast
curl.exe http://localhost:PUERTO/openapi/v1.json

# Mostrar status y headers
curl.exe -i http://localhost:PUERTO/weatherforecast

# Seguir redirecciones y aceptar certificado local solo en desarrollo
curl.exe -k -L http://localhost:PUERTO/weatherforecast
```

## `.csproj` esencial

Archivo:

```text
src/CustomerService.Api/CustomerService.Api.csproj
```

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.x" />
  </ItemGroup>
</Project>
```

- `TargetFramework`: plataforma objetivo y APIs disponibles.
- `Nullable`: activa análisis de referencias anulables.
- `ImplicitUsings`: agrega imports comunes automáticamente.
- `PackageReference`: declara una dependencia NuGet.

## `Program.cs` esencial

Archivo:

```text
src/CustomerService.Api/Program.cs
```

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

Recordatorio:

```text
builder.Services... → registra servicios en DI
builder.Build()      → construye la aplicación
app.Use...           → incorpora middleware
app.Map...           → publica endpoints
app.Run()            → inicia el servidor
```

## Controllers

```csharp
[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    [HttpGet("{id:long}", Name = "GetCustomerById")]
    public ActionResult GetById(long id)
    {
        return Ok(new { Id = id });
    }
}
```

Ruta resultante:

```http
GET /api/customers/42
```

- `[ApiController]`: habilita comportamientos específicos de API.
- `[Route]`: establece la ruta base.
- `[HttpGet]`: asigna el método HTTP y una plantilla adicional.
- `Name`: nombra el endpoint; no cambia por sí solo la URL.

## Propiedades y creación de objetos

```csharp
public class WeatherForecast
{
    public DateOnly Date { get; set; }
    public int TemperatureC { get; set; }
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
    public string? Summary { get; set; }
}
```

```csharp
var forecast = new WeatherForecast
{
    Date = DateOnly.FromDateTime(DateTime.Now),
    TemperatureC = 20,
    Summary = "Warm"
};
```

- `{ get; set; }`: propiedad legible y modificable.
- `string?`: puede ser `null`.
- `TemperatureF => ...`: propiedad calculada de solo lectura.
- `new WeatherForecast`: instanciación directa, no inyección.
- El serializador JSON lee las propiedades públicas.

## Dependency Injection frente a `new`

```csharp
public CustomersController(ICustomerService customerService)
{
    _customerService = customerService;
}
```

`ICustomerService` llega mediante DI. Un DTO o modelo creado así:

```csharp
new CustomerResponse(...)
```

se instancia directamente y no se inyecta.

## OpenAPI frente a respuesta normal

| Endpoint normal | Documento OpenAPI |
|---|---|
| Devuelve datos de negocio | Describe el contrato |
| Cambia según la operación | Cambia al modificar la API |
| Lo consume el cliente | Lo consumen herramientas y desarrolladores |
| `/weatherforecast` | `/openapi/v1.json` |

OpenAPI describe rutas, operaciones, parámetros, schemas, respuestas y seguridad.

## Configuración

- `appsettings.json`: configuración general.
- `appsettings.Development.json`: sobrescribe valores en Development.
- `Properties/launchSettings.json`: perfiles locales de inicio y depuración.
- `launchSettings.json` no configura por sí solo producción.

Perfil con HTTP y HTTPS:

```json
"applicationUrl": "https://localhost:7013;http://localhost:5162"
```

Con `app.UseHttpsRedirection()`, HTTP puede responder `307` y redirigir a HTTPS.

```powershell
dotnet dev-certs https --trust
```

## Controllers frente a Minimal APIs

```csharp
// Controller
[HttpGet("{id:long}")]
public ActionResult GetById(long id) => Ok(new { Id = id });

// Minimal API
app.MapGet("/api/customers/{id:long}",
    (long id) => Results.Ok(new { Id = id }));
```

- Controllers: clases, atributos y convenciones MVC.
- Minimal APIs: `MapGet`, `MapPost`, `MapPut`, `MapDelete`.
- Ambos pueden utilizar DI, devolver JSON y usarse profesionalmente.

## Carpetas, namespaces y assemblies

```text
Carpeta                         Namespace habitual
Controllers/                    CustomerService.Api.Controllers
Services/                       CustomerService.Api.Services
Contracts/Customers/            CustomerService.Api.Contracts.Customers
```

```csharp
namespace CustomerService.Api.Controllers;
```

- La coincidencia carpeta/namespace es convencional, no obligatoria.
- `using` es aproximadamente equivalente a `import`.
- `internal` limita el acceso al assembly, no al namespace.
- El project se compila normalmente como un assembly DLL.

## Errores frecuentes

| Síntoma | Verificación |
|---|---|
| Project no aparece en la solution | `dotnet sln ... list` y `dotnet sln ... add` |
| Endpoint de Controller devuelve 404 | Verificar `AddControllers`, `MapControllers` y atributos de ruta |
| HTTP no muestra body | Usar `curl.exe -i`; puede ser una redirección 307 |
| HTTPS no es confiable localmente | `dotnet dev-certs https --trust` |
| Paquete no se resuelve | Revisar `.csproj`, fuentes NuGet y ejecutar `dotnet restore` |
| Framework no disponible | `dotnet --list-sdks` y `dotnet --list-runtimes` |

## Checklist de incorporación a una solución

- [ ] Identificar `.slnx` o `.sln`.
- [ ] Listar los projects incluidos.
- [ ] Revisar cada `.csproj`.
- [ ] Confirmar `TargetFramework`.
- [ ] Revisar `PackageReference` y `ProjectReference`.
- [ ] Leer `Program.cs`.
- [ ] Identificar registros de DI.
- [ ] Revisar el pipeline de middleware en orden.
- [ ] Identificar Controllers y Minimal APIs.
- [ ] Revisar `appsettings` y environments.
- [ ] Revisar perfiles locales en `launchSettings.json`.
- [ ] Ejecutar `restore`, `build`, `test` y `run` según corresponda.


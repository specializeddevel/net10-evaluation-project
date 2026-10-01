# Cheat sheet: Configuración y Options Pattern en ASP.NET Core 10

## Alcance

Proyecto:

```text
CustomerService.Api
```

Archivos principales de este bloque:

```text
src/CustomerService.Api/
├── ExceptionHandling/
│   └── GlobalExceptionHandler.cs
├── Exceptions/
│   └── CustomerMinimumAgeException.cs
├── Options/
│   └── CustomerPolicyOptions.cs
├── Services/
│   └── InMemoryCustomerService.cs
├── Program.cs
├── appsettings.json
└── appsettings.Development.json
```

Este bloque cubre:

- configuración externa en ASP.NET Core;
- `appsettings.json` y archivos por ambiente;
- jerarquía y precedencia de proveedores;
- sobrescritura mediante variables de entorno;
- Options Pattern y binding fuertemente tipado;
- validación de configuración y comportamiento *fail fast*;
- inyección de `IOptions<T>`;
- diferencias entre `IOptions<T>`, `IOptionsSnapshot<T>` e `IOptionsMonitor<T>`;
- compatibilidad entre Options y lifetimes de DI;
- uso de configuración en una regla de negocio;
- traducción de una infracción de negocio a HTTP `422`.

## Mapa rápido Java / Spring → .NET

| Java / Spring | ASP.NET Core / .NET |
|---|---|
| `application.yml` o `application.properties` | `appsettings.json` |
| `application-dev.yml` | `appsettings.Development.json` |
| Spring Profiles | ASP.NET Core Environments |
| `@ConfigurationProperties` | Options Pattern |
| Bean de propiedades tipadas | `IOptions<T>` y variantes |
| Bean Validation sobre configuración | `ValidateDataAnnotations()` |
| Validación durante el arranque | `ValidateOnStart()` |
| Propiedad jerárquica `customer.policy.minimum-age` | Clave `CustomerPolicy:MinimumAge` |
| Variable de entorno para una propiedad | `CustomerPolicy__MinimumAge` |
| Configuración refrescable / `@RefreshScope` | Aproximadamente `IOptionsMonitor<T>` |

Las equivalencias son aproximadas. El ciclo de vida de los objetos y el mecanismo de recarga no son idénticos.

## Modelo mental completo

```text
appsettings.json
appsettings.{Environment}.json
User Secrets
variables de entorno
argumentos de línea de comandos
        ↓
builder.Configuration
        ↓ selecciona una sección
GetSection("CustomerPolicy")
        ↓ binding
CustomerPolicyOptions
        ↓ validación
ValidateDataAnnotations + ValidateOnStart
        ↓ registro en DI
IOptions<CustomerPolicyOptions>
        ↓ inyección
InMemoryCustomerService
        ↓ regla de negocio
EnsureMinimumAge
        ↓ si no se cumple
CustomerMinimumAgeException
        ↓ traducción HTTP
GlobalExceptionHandler
        ↓
422 ProblemDetails
```

## El sistema de configuración

Esta línea crea el builder con proveedores de configuración predeterminados:

```csharp
var builder = WebApplication.CreateBuilder(args);
```

`builder.Configuration` no representa exclusivamente `appsettings.json`. Es una vista unificada de múltiples fuentes de configuración.

Conceptualmente:

```text
varias fuentes
      ↓
ConfigurationManager
      ↓
clave efectiva para cada configuración
```

Cuando una misma clave existe en más de una fuente, el proveedor de mayor prioridad determina el valor efectivo.

## Precedencia de configuración

Orden típico de menor a mayor prioridad para configuración de aplicación:

```text
appsettings.json
        ↓ sobrescribe
appsettings.{Environment}.json
        ↓ sobrescriben
User Secrets en Development
        ↓ sobrescriben
variables de entorno
        ↓ sobrescriben
argumentos de línea de comandos
```

Ejemplo:

```text
appsettings.json                     MinimumAge = 18
appsettings.Development.json         no define MinimumAge
variable de entorno                  MinimumAge = 21

valor efectivo                       MinimumAge = 21
```

La combinación ocurre por clave. El archivo específico del ambiente no necesita repetir todo el contenido del archivo base.

## `appsettings.json`

Configuración actual:

```json
{
  "CustomerPolicy": {
    "MinimumAge": 18
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

La ruta lógica de la propiedad es:

```text
CustomerPolicy:MinimumAge
```

Los dos puntos separan niveles dentro de la API de configuración.

## Configuración por ambiente

Archivo actual:

```text
appsettings.Development.json
```

Contenido:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

Al ejecutar con ambiente `Development`, ASP.NET Core carga:

```text
appsettings.json
        +
appsettings.Development.json
```

El segundo archivo sobrescribe solamente las claves coincidentes.

Nombres habituales:

```text
Development
Staging
Production
```

Si no se configura un ambiente, el predeterminado es `Production`.

## Variables de entorno jerárquicas

Dentro de la API de configuración se utiliza `:`:

```text
CustomerPolicy:MinimumAge
```

En variables de entorno se utiliza `__` para mantener compatibilidad entre plataformas:

```text
CustomerPolicy__MinimumAge
```

Conversión automática:

```text
CustomerPolicy__MinimumAge
              ↓
CustomerPolicy:MinimumAge
```

Prueba temporal en PowerShell:

```powershell
$env:CustomerPolicy__MinimumAge = "21"
```

Comprobar:

```powershell
$env:CustomerPolicy__MinimumAge
```

Eliminar al finalizar:

```powershell
Remove-Item Env:CustomerPolicy__MinimumAge
```

Comprobar que desapareció:

```powershell
Test-Path Env:CustomerPolicy__MinimumAge
```

Resultado esperado:

```text
False
```

Una variable creada de esta forma afecta al proceso iniciado desde esa terminal y a sus procesos hijos. No modifica `appsettings.json`.

## Por qué usar Options Pattern

Lectura directa, débilmente tipada:

```csharp
string? value =
    builder.Configuration["CustomerPolicy:MinimumAge"];
```

Problemas:

- la clave es un string susceptible a errores;
- el resultado inicial es texto;
- no existe una agrupación explícita de valores relacionados;
- el consumidor queda acoplado a toda la configuración;
- la validación puede dispersarse;
- los errores pueden descubrirse tarde.

Acceso con Options Pattern:

```csharp
int minimumAge = options.Value.MinimumAge;
```

Beneficios:

- tipo conocido en compilación;
- IntelliSense;
- encapsulación por escenario;
- validación centralizada;
- inyección de solamente la configuración necesaria;
- separación entre fuente de configuración y consumidor.

## Clase Options

Archivo:

```text
src/CustomerService.Api/Options/CustomerPolicyOptions.cs
```

Contenido:

```csharp
using System.ComponentModel.DataAnnotations;

namespace CustomerService.Api.Options;

public sealed class CustomerPolicyOptions
{
    public const string SectionName = "CustomerPolicy";

    [Range(
        1,
        120,
        ErrorMessage = "MinimumAge must be between 1 and 120.")]
    public int MinimumAge { get; set; }
}
```

Responsabilidades:

| Elemento | Propósito |
|---|---|
| `CustomerPolicyOptions` | tipo C# que representa la sección |
| `SectionName` | evita repetir el string de la sección |
| `MinimumAge` | destino tipado del valor configurado |
| `[Range]` | valida el valor después del binding |

Una clase Options normalmente:

- no es abstracta;
- tiene propiedades públicas asignables;
- agrupa valores relacionados por escenario;
- no conoce la fuente concreta de configuración.

`SectionName` es una constante, no una propiedad, y no participa del binding.

## Tipo C# frente a sección JSON

En:

```csharp
.AddOptions<CustomerPolicyOptions>()
```

`CustomerPolicyOptions` es el tipo genérico C#.

En:

```csharp
.GetSection(CustomerPolicyOptions.SectionName)
```

`CustomerPolicyOptions.SectionName` contiene el nombre de la sección JSON:

```text
CustomerPolicy
```

No es obligatorio que ambos nombres sean idénticos:

```text
Tipo C#:       CustomerPolicyOptions
Sección JSON:  CustomerPolicy
```

El sufijo `Options` es una convención.

La carpeta tampoco determina el tipo. Lo hace el namespace:

```csharp
namespace CustomerService.Api.Options;
```

Por eso `Program.cs` importa:

```csharp
using CustomerService.Api.Options;
```

Sin el `using`, el nombre completo sería:

```csharp
CustomerService.Api.Options.CustomerPolicyOptions
```

## Registro, binding y validación

Registro actual en `Program.cs`:

```csharp
builder.Services
    .AddOptions<CustomerPolicyOptions>()
    .Bind(builder.Configuration.GetSection(
        CustomerPolicyOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

### `AddOptions<T>()`

```csharp
.AddOptions<CustomerPolicyOptions>()
```

Crea un `OptionsBuilder<CustomerPolicyOptions>` y registra la infraestructura necesaria en DI.

### `GetSection(...)`

```csharp
builder.Configuration.GetSection(
    CustomerPolicyOptions.SectionName)
```

Selecciona la sección lógica:

```text
CustomerPolicy
```

### `Bind(...)`

```csharp
.Bind(...)
```

Asigna valores por correspondencia de nombres:

```text
JSON MinimumAge
        ↓
propiedad C# MinimumAge
```

Las claves de configuración no distinguen mayúsculas y minúsculas, aunque conviene mantener nombres consistentes.

### `ValidateDataAnnotations()`

```csharp
.ValidateDataAnnotations()
```

Activa los atributos como `[Range]` sobre la clase Options.

En un proyecto basado en `Microsoft.NET.Sdk.Web`, la infraestructura de `Microsoft.Extensions.Options.DataAnnotations` está disponible mediante el framework compartido. Este laboratorio no necesitó agregar un paquete NuGet explícito.

### `ValidateOnStart()`

```csharp
.ValidateOnStart()
```

Obliga a validar durante el arranque, antes de aceptar tráfico.

Sin `ValidateOnStart`, la validación puede ocurrir recién cuando algún componente solicite el valor de Options.

## Validación *fail fast*

Configuración válida:

```json
"MinimumAge": 18
```

La aplicación arranca.

Configuración inválida:

```json
"MinimumAge": 0
```

La aplicación falla al arrancar con una excepción similar a:

```text
Microsoft.Extensions.Options.OptionsValidationException:
DataAnnotation validation failed for
'CustomerPolicyOptions' members: 'MinimumAge'
with the error:
'MinimumAge must be between 1 and 120.'
```

Distinción importante:

```text
dotnet build
    comprueba código, tipos y compilación

dotnet run
    carga archivos y proveedores de configuración
    ejecuta la validación de Options
```

Por eso una configuración inválida puede compilar correctamente y fallar durante el arranque.

No se genera una respuesta HTTP porque el servidor todavía no está escuchando solicitudes. `GlobalExceptionHandler` tampoco participa: su middleware pertenece al pipeline HTTP, que aún no está operativo.

## DataAnnotations en DTO frente a Options

El mismo mecanismo de atributos puede intervenir en contextos distintos:

| Ubicación | Orquestador | Momento | Resultado típico |
|---|---|---|---|
| DTO HTTP | MVC | durante una petición | `400 ValidationProblemDetails` |
| clase Options | infraestructura Options | arranque o acceso | `OptionsValidationException` |

Ejemplo DTO:

```csharp
[StringLength(100)]
string FirstName
```

Ejemplo Options:

```csharp
[Range(1, 120)]
public int MinimumAge { get; set; }
```

Que ambos utilicen DataAnnotations no significa que MVC valide la configuración.

## Inyección mediante `IOptions<T>`

Usings del servicio:

```csharp
using CustomerService.Api.Options;
using Microsoft.Extensions.Options;
```

Constructor actual:

```csharp
private readonly CustomerPolicyOptions _customerPolicyOptions;

public InMemoryCustomerService(
    IOptions<CustomerPolicyOptions> customerPolicyOptions)
{
    _customerPolicyOptions = customerPolicyOptions.Value;
}
```

DI puede resolver `IOptions<CustomerPolicyOptions>` porque el tipo fue registrado mediante `AddOptions<CustomerPolicyOptions>()`.

`IOptions<T>` es un contenedor. La instancia concreta se obtiene con:

```csharp
customerPolicyOptions.Value
```

`AddOptions<T>()` no registra automáticamente `T` para inyección directa. Por eso se inyecta:

```csharp
IOptions<CustomerPolicyOptions>
```

y no:

```csharp
CustomerPolicyOptions
```

## Las tres interfaces principales

| Interfaz | Lifetime en DI | Acceso | Recarga | Named options | Inyectable en singleton |
|---|---:|---|---:|---:|---:|
| `IOptions<T>` | Singleton | `Value` | No | No | Sí |
| `IOptionsSnapshot<T>` | Scoped | `Value` | Entre scopes/requests | Sí | No |
| `IOptionsMonitor<T>` | Singleton | `CurrentValue` | Sí, si el proveedor lo permite | Sí | Sí |

## `IOptions<T>`

Características:

- se registra como singleton;
- puede inyectarse en cualquier lifetime;
- no relee configuración después de iniciada la aplicación;
- ofrece `Value`;
- representa una política estable durante la ejecución.

Secuencia:

```text
arranque con MinimumAge = 18
        ↓
IOptions.Value = objeto con 18
        ↓
el archivo cambia a 21
        ↓
el servicio continúa usando 18
        ↓
reinicio
        ↓
el servicio usa 21
```

Elección actual:

```csharp
IOptions<CustomerPolicyOptions>
```

Es adecuada porque la política debe ser consistente durante la vida del proceso y el consumidor es singleton.

## `IOptionsSnapshot<T>`

Características:

- se registra como scoped;
- es útil cuando las opciones deben recalcularse por request;
- conserva el mismo valor dentro de un scope;
- una petición posterior puede observar cambios;
- no puede inyectarse en un singleton.

Modelo:

```text
Request A
└── scope A
    └── IOptionsSnapshot → MinimumAge = 18

Request B
└── scope B
    └── IOptionsSnapshot → MinimumAge = 21
```

El servicio actual está registrado así:

```csharp
builder.Services.AddSingleton<
    ICustomerService,
    InMemoryCustomerService>();
```

Intentar inyectarle un snapshot viola los lifetimes:

```text
singleton
    intenta conservar
        ↓
dependencia scoped de una petición
```

Error típico:

```text
Cannot consume scoped service
'IOptionsSnapshot<CustomerPolicyOptions>'
from singleton 'ICustomerService'.
```

Cambiar el servicio a scoped tampoco es una solución neutra. En el diseño actual, `_customers` pertenece a la instancia del servicio; una instancia nueva por request crearía una lista nueva y perdería los cambios en memoria.

## `IOptionsMonitor<T>`

Características:

- se registra como singleton;
- puede inyectarse en cualquier lifetime;
- expone `CurrentValue`;
- admite configuración recargable;
- permite notificaciones mediante `OnChange`;
- es apropiado cuando un singleton necesita valores actuales.

Uso dinámico correcto:

```csharp
private readonly IOptionsMonitor<CustomerPolicyOptions> _options;

public InMemoryCustomerService(
    IOptionsMonitor<CustomerPolicyOptions> options)
{
    _options = options;
}
```

Consultar al aplicar la regla:

```csharp
int minimumAge = _options.CurrentValue.MinimumAge;
```

Suscripción conceptual:

```csharp
options.OnChange(updatedOptions =>
{
    // Reaccionar al nuevo valor.
});
```

No todos los proveedores soportan recarga. Los archivos JSON pueden notificar cambios; no debe suponerse lo mismo para cualquier fuente externa o variable de entorno.

## Error habitual con `CurrentValue`

Esto captura una instancia y pierde el comportamiento dinámico:

```csharp
public InMemoryCustomerService(
    IOptionsMonitor<CustomerPolicyOptions> options)
{
    _customerPolicyOptions = options.CurrentValue;
}
```

Secuencia:

```text
constructor guarda objeto con 18
        ↓
monitor crea un objeto nuevo con 21
        ↓
el campo continúa apuntando al objeto anterior con 18
```

Si se necesita recarga, debe guardarse el monitor y consultar `CurrentValue` en cada uso.

## Decisión actual de lifetime

```text
InMemoryCustomerService      Singleton
IOptions<T>                  Singleton
CustomerPolicyOptions        estable durante el proceso
```

Razones:

- lifetimes compatibles;
- configuración validada al arrancar;
- política consistente entre peticiones;
- no se requiere recarga dinámica;
- el servicio mantiene estado en memoria.

La elección no depende solo de que el código compile:

```text
lifetime del consumidor
        +
necesidad de recarga
        +
consistencia requerida
        =
interfaz Options apropiada
```

## De configuración a regla de negocio

La clase Options contiene datos de configuración. No aplica la regla por sí misma.

```text
CustomerPolicyOptions
    contiene MinimumAge

InMemoryCustomerService
    aplica la regla

CustomerMinimumAgeException
    representa el incumplimiento

GlobalExceptionHandler
    traduce la excepción a HTTP
```

Método actual:

```csharp
private void EnsureMinimumAge(DateOnly birthDate)
{
    DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

    DateOnly maximumAllowedBirthDate =
        today.AddYears(-_customerPolicyOptions.MinimumAge);

    if (birthDate > maximumAllowedBirthDate)
    {
        throw new CustomerMinimumAgeException(
            _customerPolicyOptions.MinimumAge);
    }
}
```

Ejemplo conceptual:

```text
fecha actual                  2026-09-29
edad mínima                   18
fecha máxima permitida        2008-09-29
```

La comparación es:

```csharp
birthDate > maximumAllowedBirthDate
```

Una persona nacida exactamente en la fecha límite ya cumple la edad, por eso no se utiliza `>=`.

## Orden de validaciones en `Create`

```csharp
EnsureMinimumAge(request.BirthDate);

EnsureUnique(
    request.Email,
    request.DocumentNumber);
```

Primero se valida una regla simple y luego se recorre la colección para comprobar unicidad.

## Orden de validaciones en `Update`

```csharp
Customer? customer = GetById(id);

if (customer is null)
{
    return null;
}

EnsureMinimumAge(request.BirthDate);

EnsureUnique(
    request.Email,
    request.DocumentNumber,
    id);
```

Primero se determina si el recurso existe. De lo contrario, una petición para un ID inexistente podría devolver una infracción de edad en vez de `404`.

Precisión: la validación estructural del DTO realizada por MVC ocurre antes de entrar al servicio. Un body estructuralmente inválido puede producir `400` antes de comprobar la existencia.

## Excepción de negocio

Archivo:

```text
src/CustomerService.Api/Exceptions/CustomerMinimumAgeException.cs
```

Contenido:

```csharp
namespace CustomerService.Api.Exceptions;

public sealed class CustomerMinimumAgeException : Exception
{
    public CustomerMinimumAgeException(int minimumAge)
        : base(
            $"Customer must be at least {minimumAge} years old.")
    {
        MinimumAge = minimumAge;
    }

    public int MinimumAge { get; }
}
```

La excepción conserva:

- `Message`, para una descripción comprensible;
- `MinimumAge`, para producir información estructurada.

El namespace es obligatorio para ubicar el tipo dentro de `CustomerService.Api.Exceptions`. Sin él, la clase queda en el namespace global y puede incluso compilar, pero rompe la organización esperada.

## Traducción a HTTP `422`

Caso del handler:

```csharp
case CustomerMinimumAgeException minimumAgeException:
    statusCode = StatusCodes.Status422UnprocessableEntity;
    title = "Customer policy violation";
    detail = minimumAgeException.Message;

    _logger.LogWarning(
        "Customer minimum age policy rejected request. " +
        "MinimumAge: {MinimumAge}, TraceId: {TraceId}, " +
        "RequestId: {RequestId}",
        minimumAgeException.MinimumAge,
        traceId,
        httpContext.TraceIdentifier);
    break;
```

Extensión estructurada de `ProblemDetails`:

```csharp
if (exception is CustomerMinimumAgeException ageException)
{
    problemDetails.Extensions["minimumAge"] =
        ageException.MinimumAge;
}
```

Respuesta aproximada:

```json
{
  "title": "Customer policy violation",
  "status": 422,
  "detail": "Customer must be at least 18 years old.",
  "instance": "/api/customers",
  "minimumAge": 18,
  "traceId": "..."
}
```

ASP.NET Core conserva la constante histórica:

```csharp
StatusCodes.Status422UnprocessableEntity
```

RFC 9110 utiliza el nombre *Unprocessable Content*.

## `400`, `409`, `422` y `500`

| Estado | Situación en el proyecto | Responsable principal |
|---:|---|---|
| `400` | JSON no convertible o DTO inválido | MVC / model binding / validación |
| `409` | email o documento duplicado | servicio + handler global |
| `422` | DTO válido que viola la política de edad | servicio + handler global |
| `500` | excepción inesperada | handler global |

`422` es apropiado porque el servidor entiende la representación, pero no puede procesarla debido a una regla semántica del negocio.

## Documentación OpenAPI

En POST y PUT se agregó:

```csharp
[ProducesResponseType<ProblemDetails>(
    StatusCodes.Status422UnprocessableEntity)]
```

Este atributo documenta el contrato. No ejecuta la validación ni genera por sí mismo la respuesta.

La secuencia real es:

```text
servicio lanza excepción
        ↓
handler produce 422
        ↓
ProducesResponseType permite que OpenAPI lo anuncie
        ↓
Swagger UI y Scalar lo muestran
```

## Pruebas reproducibles

Directorio de trabajo:

```text
C:\Users\RaulBurgos\source\repos\dotnet-course
```

### Compilar

```powershell
dotnet build .\CustomerService.slnx
```

### Ejecutar

```powershell
dotnet run --project .\src\CustomerService.Api\CustomerService.Api.csproj --launch-profile https
```

### Configuración válida

```json
"MinimumAge": 18
```

Resultado esperado: la aplicación inicia.

### Configuración inválida

Cambiar temporalmente:

```json
"MinimumAge": 0
```

Resultado esperado: `OptionsValidationException` durante `dotnet run`; la API no empieza a escuchar.

Restaurar después:

```json
"MinimumAge": 18
```

### Sobrescritura por ambiente

```powershell
$env:CustomerPolicy__MinimumAge = "21"
```

Reiniciar la API y enviar un cliente mayor de 18 pero menor de 21.

Resultado esperado:

```http
422 Unprocessable Content
```

El body debe mostrar:

```json
"minimumAge": 21
```

Eliminar después:

```powershell
Remove-Item Env:CustomerPolicy__MinimumAge
```

### Cliente menor de edad

- Método: `POST`
- URL: `https://localhost:7013/api/customers`
- Body:

```json
{
  "firstName": "Young",
  "lastName": "Customer",
  "email": "young@example.com",
  "documentNumber": "UNDER-18-001",
  "birthDate": "2020-01-01"
}
```

Resultado esperado con edad mínima 18:

```http
422 Unprocessable Content
```

### Prueba de frontera

Calcular la fecha UTC actual menos 18 años:

```powershell
(Get-Date).ToUniversalTime().AddYears(-18).ToString("yyyy-MM-dd")
```

Usar exactamente esa fecha como `birthDate` y valores únicos para email/documento.

Resultado esperado:

```http
201 Created
```

## Errores frecuentes

### Confundir el tipo con la sección

Incorrecto conceptualmente:

```text
CustomerPolicyOptions debe llamarse igual que CustomerPolicy
```

Correcto:

```text
CustomerPolicyOptions    tipo C#
CustomerPolicy           sección de configuración
```

### Creer que la carpeta resuelve el tipo

La carpeta organiza archivos. El compilador utiliza namespaces, imports y referencias.

### Omitir el namespace en una excepción

Una clase puede terminar en el namespace global y compilar, pero deja de pertenecer a la organización esperada del proyecto.

### Esperar que `dotnet build` valide `appsettings.json`

La validación configurada con `ValidateOnStart()` ocurre durante el arranque, no durante la compilación.

### Esperar una respuesta HTTP ante un fallo de arranque

No existe servidor escuchando y el pipeline de middleware todavía no está operativo.

### Confundir DataAnnotations de Options con MVC

MVC valida DTOs de una petición. Options valida configuración de la aplicación.

### Inyectar `IOptionsSnapshot<T>` en un singleton

Un singleton no puede depender de un servicio scoped.

### Extraer `CurrentValue` en el constructor

Guardar `monitor.CurrentValue` captura una instancia. Para recarga dinámica debe guardarse el monitor.

### Suponer que todo proveedor se recarga

`IOptionsMonitor<T>` necesita que la fuente produzca notificaciones de cambio. No todas las fuentes lo hacen.

### Validar la edad antes de comprobar existencia en Update

Puede producir `422` para un recurso inexistente cuando el resultado de negocio esperado es `404`.

### Olvidar eliminar una variable temporal

La variable de entorno continúa sobrescribiendo el JSON mientras exista en la terminal desde la que se inicia la aplicación.

### Colocar secretos en `appsettings.json`

Los archivos versionados no deben contener contraseñas, tokens ni credenciales reales.

## Buenas prácticas de producción

- Agrupar configuración por escenario, no en una clase global gigantesca.
- Inyectar Options específicas en lugar de `IConfiguration` cuando el consumidor conoce un contrato estable.
- Validar valores obligatorios y rangos.
- Usar `ValidateOnStart()` cuando una configuración inválida impide operar de forma segura.
- Mantener secretos fuera de archivos versionados.
- Utilizar User Secrets solo para desarrollo local; en producción usar el mecanismo de secretos de la plataforma.
- Evitar registrar valores sensibles en logs o respuestas.
- Elegir `IOptions`, snapshot o monitor según lifetime y necesidad real de recarga.
- No habilitar recarga dinámica de políticas de negocio sin analizar consistencia, auditoría y concurrencia.
- Reiniciar o redesplegar deliberadamente cuando el cambio de política deba ser atómico por instancia.
- Considerar base de datos o servicio de reglas cuando una política cambie con frecuencia, dependa del cliente o requiera historial.
- Considerar `TimeProvider` para hacer deterministas las reglas dependientes del reloj.
- Definir explícitamente la zona horaria de negocio para reglas de edad o fechas civiles.

## Configuración operativa frente a datos de dominio

`appsettings.json` es apropiado para valores operativos relativamente estables:

```text
timeouts
límites técnicos
URLs de servicios
flags operativos
políticas simples por despliegue
```

Puede no ser apropiado para:

```text
reglas que cambian frecuentemente
políticas por cliente o país
valores que necesitan auditoría
datos administrados por usuarios
reglas con vigencia histórica
```

En esos casos puede ser preferible una base de datos, un servicio de configuración centralizado o un componente explícito de políticas de dominio.

## Diferencias relevantes entre .NET 8, 9 y 10

Las APIs utilizadas en este bloque mantienen el mismo enfoque en .NET 8, 9 y 10:

```csharp
AddOptions<T>()
Bind(...)
ValidateDataAnnotations()
ValidateOnStart()
IOptions<T>
IOptionsSnapshot<T>
IOptionsMonitor<T>
```

El proyecto utiliza .NET 10 como baseline. Al consultar documentación futura, verificar la versión seleccionada: funcionalidades de validación asíncrona de Options documentadas para versiones posteriores no deben asumirse disponibles en .NET 10.

## Preguntas frecuentes surgidas durante el laboratorio

### ¿El tipo de `AddOptions<CustomerPolicyOptions>()` debe ser la clase creada?

Sí. El argumento genérico es exactamente el tipo C# que representa las opciones. El nombre de la sección se selecciona por separado con `GetSection(...)`.

### ¿La carpeta `Options` hace que .NET encuentre la clase?

No. La carpeta es organización física; la resolución del tipo depende del namespace y del `using`.

### ¿Por qué no inyectar `CustomerPolicyOptions` directamente?

Porque `AddOptions<T>()` registra la infraestructura y las interfaces Options, no el tipo concreto como servicio independiente.

### ¿Por qué no leer directamente `IConfiguration` desde el servicio?

Options reduce strings mágicos, centraliza binding/validación y limita la dependencia del consumidor al grupo de valores que realmente utiliza.

### ¿Por qué `IOptionsSnapshot<T>` no sirve en el servicio actual?

Porque snapshot es scoped y `InMemoryCustomerService` es singleton. Además, volver scoped el servicio actual recrearía también su lista en memoria por petición.

### ¿Qué usar si un singleton necesita cambios dinámicos?

`IOptionsMonitor<T>`, conservando el monitor y consultando `CurrentValue` cuando se usa la configuración.

### ¿Por qué no guardar `CurrentValue` en el constructor?

Porque se conserva la instancia vigente en ese momento. El monitor puede reemplazarla posteriormente por otra instancia.

### ¿Qué significa `__` en una variable de entorno?

Representa el separador jerárquico `:` de la configuración y funciona de manera portable entre plataformas.

### ¿Por qué reiniciar después de cambiar una variable de entorno?

La variable se entrega al proceso al iniciarlo. Un proceso ya iniciado no recibe automáticamente los cambios realizados en el ambiente de su terminal padre.

### ¿Por qué `422` y no `400` para la edad mínima?

Porque el JSON y el DTO son válidos, pero el contenido no puede procesarse debido a una regla semántica de negocio.

### ¿Por qué comprobar existencia antes de edad en Update?

Porque no corresponde validar una modificación de negocio sobre un recurso inexistente; el servicio debe permitir que el controller produzca `404`.

### ¿`MinimumAge` debería estar siempre en configuración?

No necesariamente. Es un ejemplo útil para Options. En producción la ubicación depende de frecuencia de cambio, auditoría, alcance por cliente y modelo de dominio.

## Preguntas de entrevista con respuestas breves

### ¿Qué es Options Pattern?

Un mecanismo para representar grupos de configuración mediante clases fuertemente tipadas, registrarlas en DI y validarlas centralmente.

### ¿Qué aporta `ValidateOnStart()`?

Ejecuta la validación al iniciar la aplicación para impedir que una instancia con configuración inválida acepte tráfico.

### ¿Diferencia entre `IOptions<T>` e `IOptionsSnapshot<T>`?

`IOptions<T>` es singleton y estable. Snapshot es scoped y puede recomputar opciones por request.

### ¿Diferencia entre snapshot y monitor?

Snapshot es scoped y entrega una versión por scope; monitor es singleton, expone `CurrentValue` y admite notificaciones de cambio.

### ¿Puede un singleton consumir `IOptionsSnapshot<T>`?

No, porque introduciría una dependencia scoped dentro de un singleton.

### ¿Puede un singleton consumir `IOptionsMonitor<T>`?

Sí. Monitor también es singleton.

### ¿Las variables de entorno sobrescriben `appsettings.json`?

Sí, con los proveedores predeterminados de `WebApplication.CreateBuilder`.

### ¿Por qué utilizar doble guion bajo en variables jerárquicas?

Porque `__` se convierte en `:` y funciona consistentemente en las plataformas soportadas.

### ¿Options reemplaza a un sistema de secretos?

No. Options tipa y consume configuración; la protección y almacenamiento de secretos corresponde al proveedor y a la plataforma.

### ¿`ProducesResponseType(422)` genera la respuesta?

No. Solo agrega metadata OpenAPI; la respuesta real la produce el handler al capturar la excepción.

## Checklist final del bloque

- [ ] Sección `CustomerPolicy` definida en `appsettings.json`.
- [ ] Clase `CustomerPolicyOptions` ubicada en su namespace correcto.
- [ ] `SectionName` evita strings repetidos.
- [ ] Propiedades públicas compatibles con el binder.
- [ ] Rango de `MinimumAge` validado.
- [ ] Options registradas mediante `AddOptions<T>()`.
- [ ] Sección seleccionada mediante `GetSection(...)`.
- [ ] Binding configurado.
- [ ] DataAnnotations habilitadas para Options.
- [ ] `ValidateOnStart()` comprobado con una configuración inválida.
- [ ] Diferencia entre build y validación de arranque comprendida.
- [ ] Precedencia de proveedores comprendida.
- [ ] Sobrescritura mediante `CustomerPolicy__MinimumAge` comprobada.
- [ ] Variable temporal eliminada después de la prueba.
- [ ] `IOptions<T>` inyectado en el servicio.
- [ ] Lifetimes de Options comparados.
- [ ] Incompatibilidad snapshot/singleton comprendida.
- [ ] Uso correcto de `IOptionsMonitor.CurrentValue` comprendido.
- [ ] Regla de edad aplicada en Create y Update.
- [ ] Existencia comprobada antes de reglas de update.
- [ ] Excepción específica ubicada en su namespace.
- [ ] Infracción traducida a `422 ProblemDetails`.
- [ ] `minimumAge` agregado como extensión estructurada.
- [ ] `422` documentado en OpenAPI.
- [ ] Prueba negativa y prueba de frontera completadas.
- [ ] Build final sin errores.

## Referencias oficiales

- Configuración en ASP.NET Core: <https://learn.microsoft.com/aspnet/core/fundamentals/configuration/?view=aspnetcore-10.0>
- Options Pattern en ASP.NET Core: <https://learn.microsoft.com/aspnet/core/fundamentals/configuration/options?view=aspnetcore-10.0>
- Ambientes de ejecución: <https://learn.microsoft.com/aspnet/core/fundamentals/environments?view=aspnetcore-10.0>
- Safe storage of app secrets: <https://learn.microsoft.com/aspnet/core/security/app-secrets?view=aspnetcore-10.0>
- Dependency injection y lifetimes: <https://learn.microsoft.com/dotnet/core/extensions/dependency-injection#service-lifetimes>
- Códigos de estado HTTP de ASP.NET Core: <https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.statuscodes>

# Cheat sheet: Controllers, DTOs, validación, CRUD y excepciones en ASP.NET Core 10

## Alcance

Proyecto:

```text
CustomerService.Api
```

Rutas principales:

```text
src/CustomerService.Api/
├── Contracts/
├── Controllers/
├── ExceptionHandling/
├── Exceptions/
├── Mappings/
├── Models/
├── Services/
└── Program.cs
```

Este bloque cubre:

- Controllers y routing por atributos;
- model binding y fuentes de parámetros;
- DTOs, records, `required`, `init` y nullability;
- DataAnnotations y validación automática;
- mapeo manual;
- CRUD y códigos HTTP;
- duplicados y reglas de negocio;
- `ProblemDetails` e `IExceptionHandler`;
- logging estructurado, `TraceId` y `RequestId`;
- documentación OpenAPI.

## Mapa rápido Java / Spring → .NET

| Java / Spring | ASP.NET Core / C# |
|---|---|
| `@RestController` | `[ApiController]` + `ControllerBase` |
| `@RequestMapping` | `[Route]` |
| `@GetMapping` | `[HttpGet]` |
| `@PostMapping` | `[HttpPost]` |
| `@PutMapping` | `[HttpPut]` |
| `@DeleteMapping` | `[HttpDelete]` |
| `@PathVariable` | `[FromRoute]` |
| `@RequestParam` | `[FromQuery]` |
| `@RequestBody` | `[FromBody]` |
| `@RequestHeader` | `[FromHeader]` |
| Jackson / `HttpMessageConverter` | `System.Text.Json` / input formatter |
| Bean Validation + `@Valid` | DataAnnotations + validación MVC |
| `BindingResult` | `ModelState` |
| `@Service` | servicio registrado en DI |
| `@RestControllerAdvice` | `IExceptionHandler` |
| `ResponseEntity.created(uri)` | `CreatedAtAction(...)` |
| JPA dirty checking | EF Core change tracking |
| MapStruct `@MappingTarget` | mapeo sobre una instancia existente |

Las equivalencias son aproximadas; los pipelines y contratos internos no son idénticos.

## Flujo de una petición

```text
Cliente HTTP
    ↓
Kestrel
    ↓
Middleware de ASP.NET Core
    ↓
Routing selecciona el endpoint MVC
    ↓
Model binding / input formatter
    ↓
Validación MVC y ModelState
    ↓
ModelStateInvalidFilter de [ApiController]
    ├── inválido → 400 ValidationProblemDetails
    └── válido   → action del Controller
                         ↓
                      Servicio
                         ↓
                      Respuesta
```

Punto clave: la validación automática sucede antes de ejecutar el cuerpo del action, pero forma parte del pipeline interno de MVC, no de un middleware genérico.

## MVC en una Web API

MVC significa Model–View–Controller, pero una API normalmente no usa Views HTML:

```text
Model      → DTOs, contratos y modelos
View       → no utilizada en esta API
Controller → recibe operaciones HTTP y devuelve JSON
```

Registro utilizado:

```csharp
builder.Services.AddControllers();
```

No es necesario usar `AddControllersWithViews()` para una API JSON.

## Controller base

Archivo:

```text
src/CustomerService.Api/Controllers/CustomersController.cs
```

```csharp
[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
}
```

- `[ApiController]`: habilita convenciones de API, inferencia de binding y respuestas `400` automáticas.
- `[Route("api/[controller]")]`: genera la ruta base `/api/customers`.
- `ControllerBase`: base apropiada para APIs sin Views.
- Los controllers son activados por request y reciben dependencias mediante DI.

## Model binding y fuentes de parámetros

| Atributo | Fuente |
|---|---|
| `[FromRoute]` | segmento de la ruta |
| `[FromQuery]` | query string |
| `[FromBody]` | body, normalmente JSON |
| `[FromHeader]` | header HTTP |
| `[FromForm]` | formulario o multipart |
| `[FromServices]` | contenedor de DI |

Con `[ApiController]`, las reglas principales de inferencia son:

1. Un atributo explícito siempre tiene prioridad.
2. Un tipo complejo registrado en DI se obtiene desde servicios.
3. Un tipo complejo no registrado en DI se obtiene desde el body.
4. Un parámetro cuyo nombre aparece en la ruta se obtiene desde la ruta.
5. Los restantes parámetros simples se obtienen desde query string.

Ejemplo explícito:

```csharp
[HttpPut("{id:long}")]
public IActionResult Update(
    [FromRoute] long id,
    [FromBody] UpdateCustomerRequest request,
    [FromQuery] bool notify = false,
    [FromHeader(Name = "X-Correlation-ID")]
    string? correlationId = null)
```

Request correspondiente:

```http
PUT /api/customers/25?notify=true
X-Correlation-ID: operation-123
Content-Type: application/json
```

En el código actual se puede omitir `[FromRoute]` y `[FromBody]` porque la inferencia es inequívoca:

```csharp
[HttpPut("{id:long}")]
public ActionResult<CustomerResponse> Update(
    long id,
    UpdateCustomerRequest request)
```

### Reglas operativas de binding

- Solo puede leerse un parámetro desde el body por action.
- Para dos grupos de datos del body, crear un DTO que los contenga.
- Los nombres de parámetros de ruta deben coincidir, o debe usarse `Name`.
- Los headers deben declararse normalmente con `[FromHeader]`.
- JSON inválido o una conversión imposible produce un error de binding.
- Los errores de binding se almacenan junto con los de validación en `ModelState`.

Ejemplo cuando los nombres no coinciden:

```csharp
[HttpGet("{customerId:long}")]
public IActionResult GetById(
    [FromRoute(Name = "customerId")] long id)
```

## DTOs y records

### Create: record posicional

Archivo:

```text
src/CustomerService.Api/Contracts/CreateCustomerRequest.cs
```

```csharp
public sealed record CreateCustomerRequest(
    [Required]
    [StringLength(100, MinimumLength = 2)]
    string FirstName,

    [Required]
    [EmailAddress]
    string Email,

    DateOnly BirthDate);
```

En records posicionales validados por MVC, colocar los atributos sobre los parámetros del constructor primario, sin `property:`.

### Update: record no posicional

Archivo:

```text
src/CustomerService.Api/Contracts/UpdateCustomerRequest.cs
```

```csharp
public sealed record UpdateCustomerRequest
{
    [Required]
    [EmailAddress]
    public required string Email { get; init; }
}
```

- `record`: conveniente para contratos orientados a datos.
- `required`: exige inicialización y también puede formar parte del contrato requerido de `System.Text.Json`.
- `init`: solo permite asignar durante la creación/deserialización.
- `[Required]`: regla del sistema de validación; rechaza `null` y, para strings, valores vacíos.

`required` y `[Required]` no son equivalentes: uno pertenece al lenguaje/contrato de deserialización y el otro a DataAnnotations.

### Response separado

Archivo:

```text
src/CustomerService.Api/Contracts/CustomerResponse.cs
```

El consumidor no controla:

```text
Id
CreatedAt
UpdatedAt
```

Separar request, modelo y response ayuda a evitar overposting y desacopla el contrato HTTP de la entidad.

## Conversión de tipos frente a DataAnnotations

La validación de entrada tiene dos niveles:

```text
System.Text.Json / model binding
    → ¿el valor puede convertirse al tipo C#?

DataAnnotations / MVC
    → ¿el valor convertido cumple las restricciones?
```

Ejemplos:

| Entrada | Responsable | Resultado esperado |
|---|---|---|
| `"age": "abc"` para un `int` | deserialización/binding | `400` |
| `"age": 15` con `[Range(18, 130)]` | DataAnnotations | `400` |
| email con formato inválido | `[EmailAddress]` | `400` |
| email válido pero duplicado | servicio de negocio | `409` |

## DataAnnotations por tipo

### `int`

```csharp
[Required]
[Range(18, 130)]
public int? Age { get; init; }
```

Usar `int?` cuando sea necesario distinguir entre valor ausente y `0`.

### `bool`

```csharp
[Required]
public bool? IsActive { get; init; }
```

`false` es un valor válido. `[Required]` no significa “debe ser true”. Para una regla como aceptar términos se necesita una validación personalizada o `IValidatableObject`.

### `decimal`

Correspondencia habitual:

```text
Java BigDecimal → C# decimal
```

```csharp
[Required]
[Range(
    typeof(decimal),
    "0.01",
    "9999999999999999.99",
    ParseLimitsInInvariantCulture = true)]
public decimal? Amount { get; init; }
```

- `decimal` ofrece aproximadamente 28–29 dígitos significativos.
- Es apropiado para la mayoría de los importes monetarios.
- `[Range]` limita valores, pero no limita la cantidad de decimales.
- La precisión/escala necesitará una validación adicional y configuración en EF Core/SQL Server.
- C# no tiene un `BigDecimal` arbitrario incorporado equivalente al de Java.

### Record u objeto anidado

```csharp
public sealed record AddressRequest(
    [Required] string Street,
    [Required] string City,
    [RegularExpression(@"^[A-Za-z0-9 -]{4,12}$")]
    string PostalCode);
```

```csharp
[Required]
public AddressRequest? Address { get; init; }
```

MVC recorre y valida el grafo de objetos. Los errores pueden aparecer como:

```text
Address.Street
Address.PostalCode
```

La profundidad máxima de validación de MVC es 32 por defecto.

## ModelState y respuesta automática `400`

`ModelState` reúne:

- errores de binding y conversión;
- errores producidos por DataAnnotations.

Con `[ApiController]`, MVC incorpora `ModelStateInvalidFilter`, que conceptualmente hace:

```csharp
if (!ModelState.IsValid)
{
    return ValidationProblem(ModelState);
}
```

Por eso no se necesita repetir ese bloque en cada action.

Respuesta aproximada:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Email": [
      "Email format is invalid."
    ]
  },
  "traceId": "00-...-...-00"
}
```

## Mapeo manual

Archivo:

```text
src/CustomerService.Api/Mappings/CustomerMappings.cs
```

Flujos actuales:

```text
CreateCustomerRequest → Customer
UpdateCustomerRequest → Customer existente
Customer              → CustomerResponse
```

Métodos:

```csharp
request.ToModel(id, createdAt);
request.ApplyTo(customer, updatedAt);
customer.ToResponse();
```

### Por qué `ApplyTo` modifica la instancia

```csharp
public static void ApplyTo(
    this UpdateCustomerRequest request,
    Customer customer,
    DateTimeOffset updatedAt)
```

`ApplyTo` actualiza únicamente propiedades permitidas y preserva campos controlados por el servidor.

Con EF Core seguirá siendo un patrón válido:

```text
EF carga y trackea Customer
    ↓
ApplyTo modifica la misma instancia
    ↓
SaveChangesAsync detecta cambios
    ↓
SQL UPDATE
```

Un mapper automático también puede mapear sobre un objeto existente, equivalente aproximado a MapStruct con `@MappingTarget`.

En un dominio más rico se preferirá comportamiento encapsulado:

```csharp
customer.UpdateProfile(...);
```

El mapper no debe saltarse invariantes del dominio.

## `var`, tipos y `using`

Estas declaraciones producen una variable estáticamente tipada como `Customer`:

```csharp
Customer customer = _customerService.Create(request);
var customer = _customerService.Create(request);
```

`var`:

- infiere el tipo en compilación;
- no equivale a `dynamic`;
- no permite cambiar posteriormente a otro tipo;
- puede evitar un `using` cuando el nombre del tipo no aparece en el archivo.

`using CustomerService.Api.Models;` permite escribir el nombre corto `Customer`; no carga ni instancia la clase.

Regla práctica:

```text
Tipo evidente a la derecha → var suele ser claro.
Tipo importante o poco evidente → tipo explícito puede comunicar mejor.
```

## Endpoints y códigos HTTP

| Operación | Endpoint | Respuestas documentadas |
|---|---|---|
| Listar | `GET /api/customers` | `200`, `500` |
| Buscar | `GET /api/customers/{id}` | `200`, `404`, `500` |
| Crear | `POST /api/customers` | `201`, `400`, `409`, `500` |
| Actualizar | `PUT /api/customers/{id}` | `200`, `400`, `404`, `409`, `500` |
| Eliminar | `DELETE /api/customers/{id}` | `204`, `404`, `500` |

Resumen:

| Status | Uso |
|---|---|
| `200 OK` | lectura o update exitoso con body |
| `201 Created` | creación exitosa |
| `204 No Content` | eliminación exitosa sin body |
| `400 Bad Request` | binding o validación de entrada |
| `404 Not Found` | recurso inexistente |
| `409 Conflict` | conflicto con el estado actual, como duplicados |
| `500 Internal Server Error` | fallo inesperado y seguro para el cliente |

## `CreatedAtAction`

```csharp
return CreatedAtAction(
    nameof(GetById),
    new { id = customer.Id },
    response);
```

Produce aproximadamente:

```http
HTTP/1.1 201 Created
Location: https://localhost:7013/api/customers/3
Content-Type: application/json
```

El body contiene `response`.

`CreatedAtAction`:

- no crea ni guarda el cliente;
- no ejecuta `GetById`;
- usa la metadata de `GetById` para generar `Location`;
- no asigna la propiedad `CreatedAt`.

## Colecciones y DELETE conciso

```csharp
public bool Delete(long id)
{
    Customer? customer = GetById(id);
    return customer is not null && _customers.Remove(customer);
}
```

`&&` usa cortocircuito: `_customers.Remove(customer)` solo se evalúa cuando `customer is not null`.

`AsReadOnly()` protege la estructura expuesta de la colección, pero no vuelve inmutables sus elementos.

Un field `readonly` impide reasignar la referencia; no hace inmutable el contenido de un `List<T>`.

## Duplicados y exclusión del propio ID

Reglas:

- email único;
- documento único;
- comparación sin distinguir mayúsculas/minúsculas;
- espacios exteriores ignorados con `Trim()`;
- durante update se excluye al cliente actualizado.

Firma:

```csharp
private void EnsureUnique(
    string email,
    string documentNumber,
    long? excludedCustomerId = null)
```

Condición de exclusión:

```csharp
!excludedCustomerId.HasValue
    || customer.Id != excludedCustomerId.Value
```

Motivo: si el cliente `1` conserva su email, no debe encontrarse a sí mismo como duplicado. Los restantes clientes continúan participando en la comparación.

Orden en update:

```text
1. Buscar por ID.
2. Si no existe → 404.
3. Validar duplicados excluyendo el ID actual.
4. Aplicar cambios.
```

`Any(...)` equivale aproximadamente a `stream().anyMatch(...)` y deja de buscar al encontrar la primera coincidencia.

### Limitación de la validación en memoria

La comprobación previa no garantiza unicidad bajo concurrencia:

```text
Request A comprueba → no existe
Request B comprueba → no existe
Request A inserta
Request B inserta
```

Con SQL Server se necesitarán:

1. validación amigable en la aplicación;
2. índice o constraint `UNIQUE` como garantía definitiva;
3. traducción de la excepción de persistencia si ocurre una carrera.

El `List<Customer>` singleton actual tampoco es thread-safe; es una implementación temporal de aprendizaje.

## Excepciones específicas de negocio

Archivos:

```text
src/CustomerService.Api/Exceptions/DuplicateCustomerEmailException.cs
src/CustomerService.Api/Exceptions/DuplicateCustomerDocumentException.cs
```

Ventajas de tipos específicos:

- el handler distingue por tipo, no por texto;
- el status HTTP no queda acoplado al mensaje;
- las reglas son explícitas;
- las pruebas pueden verificar la excepción exacta.

No incluir email o documento en mensajes/logs evita exponer datos personales innecesariamente.

## Manejo global con `IExceptionHandler`

Archivo:

```text
src/CustomerService.Api/ExceptionHandling/GlobalExceptionHandler.cs
```

Registro en `Program.cs`:

```csharp
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
```

Middleware:

```csharp
app.UseExceptionHandler();
```

Diferencia:

```text
AddExceptionHandler → registra el servicio en DI.
UseExceptionHandler → incorpora la captura de excepciones al pipeline.
```

Contrato:

```csharp
ValueTask<bool> TryHandleAsync(
    HttpContext httpContext,
    Exception exception,
    CancellationToken cancellationToken)
```

Resultado:

| Retorno | Significado |
|---|---|
| `true` | la excepción fue manejada y el handler se hace responsable de la respuesta |
| `false` | puede intentarse otro handler o fallback |

Mapeo actual:

```text
DuplicateCustomerEmailException    → 409
DuplicateCustomerDocumentException → 409
Otra excepción                     → 500 seguro
```

## `ProblemDetails`

Respuesta de conflicto aproximada:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Customer conflict",
  "status": 409,
  "detail": "A customer with the same email already exists.",
  "instance": "/api/customers",
  "traceId": "00-...-...-00"
}
```

Respuesta inesperada segura:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
  "title": "Unexpected error",
  "status": 500,
  "detail": "An unexpected error occurred.",
  "instance": "/api/customers",
  "traceId": "00-...-...-00"
}
```

Un `500` no debe exponer:

- stack trace;
- nombres internos de clases;
- rutas del servidor;
- detalles de base de datos;
- secretos o datos personales.

La excepción completa sí debe registrarse internamente.

## Logging estructurado

```csharp
_logger.LogError(
    exception,
    "Unhandled exception processing {HttpMethod} {RequestPath}. " +
    "TraceId: {TraceId}, RequestId: {RequestId}",
    httpContext.Request.Method,
    httpContext.Request.Path,
    traceId,
    httpContext.TraceIdentifier);
```

Los placeholders son propiedades estructuradas, no interpolación textual:

```text
HttpMethod
RequestPath
TraceId
RequestId
```

Usar `LogWarning` para conflictos esperados y `LogError(exception, ...)` para errores inesperados.

## `TraceId`, `SpanId`, `RequestId` y futura correlación

| Identificador | Alcance | Propagación |
|---|---|---|
| W3C Trace ID | operación distribuida completa | automática mediante `traceparent` |
| Span ID | operación individual dentro de la traza | parte del contexto W3C |
| `HttpContext.TraceIdentifier` / RequestId | request local de Kestrel | no es la traza distribuida |
| `X-Correlation-ID` | correlación funcional propia | deberá recibirse/generarse y propagarse |

Formato de `Activity.Current.Id`:

```text
00-6964e1ab72195a650f328cd3eaac5458-28614f5aa9602c2c-00
│  │                                │                └─ flags
│  │                                └─ span-id
│  └─ trace-id
└─ versión
```

Selección actual:

```csharp
string traceId =
    Activity.Current?.Id
    ?? httpContext.TraceIdentifier;
```

El writer predeterminado de `ProblemDetails` utiliza la misma estrategia. El log debe usar el mismo valor para poder correlacionarse con la respuesta.

`RequestId` puede conservarse en logs para diagnóstico local, pero no es el identificador principal entre microservicios.

## Diferencias de diagnósticos .NET 8/9/10

`IExceptionHandler` está disponible desde ASP.NET Core 8.

```text
.NET 8/9
El middleware emitía diagnósticos incluso cuando TryHandleAsync devolvía true.

.NET 10
Los diagnósticos del middleware se suprimen por defecto cuando la excepción
fue manejada por IExceptionHandler.
```

Para restaurar el comportamiento anterior:

```csharp
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    SuppressDiagnosticsCallback = context => false
});
```

No usarlo si el handler ya registra todo y produciría duplicados innecesarios.

## `ProducesResponseType`: documentación, no ejecución

```csharp
[ProducesResponseType<ProblemDetails>(
    StatusCodes.Status409Conflict)]
```

Este atributo:

- agrega metadata;
- documenta el status y schema en OpenAPI;
- no captura excepciones;
- no obliga al action a devolver ese status;
- no reemplaza `IExceptionHandler`.

```text
ProducesResponseType   → describe.
Controller/handler/MVC → ejecutan el comportamiento real.
```

## OpenAPI, Swagger UI y Scalar

Configuración actual:

```csharp
builder.Services.AddOpenApi();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
```

Documento:

```text
https://localhost:7013/openapi/v1.json
```

Conceptos:

| Concepto | Función |
|---|---|
| OpenAPI | especificación del contrato HTTP |
| documento OpenAPI | JSON/YAML con operaciones y schemas |
| Swagger UI | interfaz web que consume OpenAPI |
| Swashbuckle | bibliotecas .NET relacionadas con Swagger/OpenAPI |
| Scalar | interfaz moderna alternativa para OpenAPI |

Desde .NET 9, el template incluye generación OpenAPI oficial, pero Swagger UI ya no viene configurado de manera predeterminada. En .NET 10 el documento generado usa OpenAPI 3.1 por defecto.

Laboratorio planificado:

```text
Microsoft.AspNetCore.OpenApi
        ↓
/openapi/v1.json
        ├── Swagger UI
        └── Scalar
```

Las UIs deben habilitarse solo de manera deliberada, normalmente en Development.

## Comandos operativos

Directorio:

```text
C:\Users\RaulBurgos\source\repos\dotnet-course
```

Compilar:

```powershell
dotnet build CustomerService.slnx
```

Ejecutar con HTTPS:

```powershell
dotnet run --project src/CustomerService.Api/CustomerService.Api.csproj --launch-profile https
```

URLs:

```text
https://localhost:7013
http://localhost:5162
```

Detener:

```text
Ctrl+C
```

## Pruebas esenciales en Postman

Headers para requests JSON:

```text
Content-Type: application/json
Accept: application/json
```

Checklist:

- [ ] Crear cliente válido → `201` y header `Location`.
- [ ] Body inválido → `400 ValidationProblemDetails`.
- [ ] Buscar ID inexistente → `404`.
- [ ] Email duplicado al crear → `409`.
- [ ] Documento duplicado al crear → `409`.
- [ ] Update conservando email/documento propio → `200`.
- [ ] Update usando datos de otro cliente → `409`.
- [ ] Update de ID inexistente con datos duplicados → `404`.
- [ ] Delete existente → `204` sin body.
- [ ] Delete inexistente → `404`.
- [ ] Error inesperado controlado → `500` seguro, sin stack trace en el body.
- [ ] `TraceId` de respuesta coincide con la propiedad estructurada del log.
- [ ] OpenAPI documenta `400`, `404`, `409` y `500` donde corresponde.

## Errores frecuentes

| Síntoma | Causa probable | Corrección |
|---|---|---|
| El action se ejecuta con DTO inválido | falta `[ApiController]` o se suprimió el filtro | revisar atributos y `ApiBehaviorOptions` |
| Un parámetro llega desde query y se esperaba body | es un tipo simple sin `[FromBody]` | declarar la fuente explícitamente |
| Fallo por dos parámetros complejos | ambos fueron inferidos como body | agruparlos en un DTO |
| Update rechaza el mismo email del cliente | no se excluyó el ID actual | pasar `excludedCustomerId` |
| Se devuelve `500` para duplicados | falta handler o mapeo de excepción | registrar `IExceptionHandler` y middleware |
| El handler está registrado pero no se ejecuta | falta `app.UseExceptionHandler()` | agregar el middleware temprano |
| OpenAPI muestra `409`, pero runtime devuelve `500` | `ProducesResponseType` solo documenta | implementar manejo real |
| Response y log muestran IDs distintos | se mezcló Activity ID con RequestId | usar el mismo TraceId y registrar RequestId aparte |
| `var` permite un tipo inesperado | confusión con `dynamic` | recordar que `var` es inferencia estática |
| Swagger no aparece en `/swagger` | .NET 10 solo genera OpenAPI por defecto | instalar/configurar una UI explícitamente |

## Decisiones arquitectónicas actuales

- El servicio devuelve `Customer`; el controller mapea a `CustomerResponse`.
- El contrato HTTP de salida no se filtra hacia el servicio.
- El servicio recibe todavía request DTOs como concesión temporal.
- Más adelante se implementará `Request → Command → Domain/Entity → Result/Response`.
- El mapeo manual permanece hasta comprender sus límites y seleccionar un mapper moderno.
- `ApplyTo` actualiza solamente campos permitidos.
- `EnsureUnique` vive por ahora en el servicio que posee la colección.
- La base de datos garantizará la unicidad cuando se incorpore EF Core.
- `ProblemDetails` es el formato uniforme de errores HTTP.
- El detalle interno de excepciones inesperadas solo se registra en logs.
- `TraceId` es el identificador principal de correlación distribuida.
- `RequestId` se conserva solo para diagnóstico local.

## Anti-patterns a evitar

- Exponer entidades directamente como contrato HTTP sin una decisión consciente.
- Aceptar `Id`, `CreatedAt` o `UpdatedAt` desde un create request.
- Repetir `if (!ModelState.IsValid)` en cada action con `[ApiController]`.
- Atrapar todas las excepciones dentro de cada controller.
- Decidir el status analizando el texto de una excepción.
- Devolver stack traces o mensajes de base de datos al cliente.
- Confiar solo en una consulta previa para garantizar unicidad concurrente.
- Reemplazar una entidad trackeada completa sin controlar overposting.
- Confundir `required`, `[Required]` y non-nullable reference types.
- Confundir `var` con `dynamic`.
- Usar `ProducesResponseType` como si implementara lógica.

## Preguntas frecuentes surgidas durante el laboratorio

### ¿Quién crea y valida el DTO?

MVC coordina model binding, el input formatter JSON y la validación. `[ApiController]` devuelve `400` antes de ejecutar el action cuando `ModelState` es inválido.

### ¿MVC significa Model–View–Controller aunque sea una API?

Sí. En una API usamos principalmente Model y Controller; no necesitamos Views Razor.

### ¿Por qué `var` puede eliminar un `using`?

Porque el código ya no menciona el nombre corto del tipo. El compilador infiere el tipo completo desde la expresión derecha.

### ¿Cómo se valida un `int`, `bool` o `decimal`?

El tipo controla la conversión; DataAnnotations controla presencia y rango. Usar tipos nullable con `[Required]` cuando deba distinguirse ausencia de valores predeterminados.

### ¿Se validan records anidados?

Sí. MVC recorre el grafo de objetos y ejecuta los validadores de sus propiedades/elementos.

### ¿Qué hace `CreatedAtAction`?

Devuelve `201`, agrega `Location` usando la ruta de otro action e incluye el response en el body. No ejecuta ese otro action.

### ¿Hay que escribir siempre `[FromBody]` y `[FromRoute]`?

No con `[ApiController]` cuando la inferencia sea clara. Sí cuando haya ambigüedad o para headers y contratos que necesiten ser explícitos.

### ¿Por qué se excluye el ID en un update?

Para que la entidad no detecte sus propios email/documento actuales como duplicados. Los demás IDs siguen siendo comparados.

### ¿`ApplyTo` es solo pedagógico?

No. Con EF Core es normal modificar una entidad trackeada y guardar mediante `SaveChangesAsync`. Un mapper también puede actualizar un destino existente.

### ¿Por qué conservar `RequestId` si ya existe `TraceId`?

`TraceId` sirve para correlación distribuida. `RequestId` puede ayudar a diagnosticar la petición local en Kestrel; no debe confundirse con la traza completa.

### ¿Swagger desapareció?

No. ASP.NET Core 10 genera OpenAPI oficialmente, pero no instala una UI interactiva por defecto. Swagger UI o Scalar deben configurarse de manera explícita.

## Preguntas de entrevista con respuestas breves

### ¿Qué aporta `[ApiController]`?

Routing por atributos obligatorio, inferencia de binding sources, `400` automático por `ModelState`, convenciones de errores y metadata específica de APIs.

### ¿Cuál es la diferencia entre binding y validation?

Binding obtiene y convierte datos del request. Validation comprueba restricciones sobre el objeto resultante.

### ¿Por qué usar DTOs separados?

Para estabilizar contratos, evitar overposting, controlar campos expuestos y desacoplar HTTP de persistencia/dominio.

### ¿Cuándo usar `409 Conflict`?

Cuando la representación es válida, pero la operación entra en conflicto con el estado actual del recurso, como una clave de negocio duplicada.

### ¿Qué diferencia hay entre `ProblemDetails` y `ValidationProblemDetails`?

`ProblemDetails` representa un problema HTTP general. `ValidationProblemDetails` agrega un diccionario de errores por campo.

### ¿Qué significa que `IExceptionHandler` devuelva `true`?

Que la excepción fue manejada y el handler se responsabilizó de producir la respuesta.

### ¿Por qué se necesita un índice único si la aplicación valida duplicados?

Porque dos requests concurrentes pueden superar la comprobación previa. Solo la base puede garantizar la unicidad atómicamente.

### ¿Qué diferencia existe entre `TraceId` y `SpanId`?

El Trace ID identifica toda la operación distribuida; cada Span ID identifica una operación individual dentro de esa traza.

## Checklist final del bloque

- [ ] Controllers registrados con `AddControllers` y publicados con `MapControllers`.
- [ ] `[ApiController]` y rutas por atributos configuradas.
- [ ] IDs `long` en todas las capas.
- [ ] Requests y responses separados.
- [ ] DataAnnotations aplicadas en la ubicación correcta para cada record.
- [ ] Model binding entendido para route, query, body y headers.
- [ ] Validación estructural diferenciada de reglas de negocio.
- [ ] Mapeos `ToModel`, `ApplyTo` y `ToResponse` comprendidos.
- [ ] `CreatedAtAction` produce `201` y `Location`.
- [ ] Duplicados producen `409`.
- [ ] Update excluye el propio ID.
- [ ] Excepciones específicas registradas.
- [ ] `AddProblemDetails` y `AddExceptionHandler` registrados.
- [ ] `UseExceptionHandler` incorporado al pipeline.
- [ ] `500` no expone detalles internos.
- [ ] Logging estructurado incluye TraceId y RequestId.
- [ ] Diferencia de diagnósticos .NET 8/9/10 comprendida.
- [ ] `ProducesResponseType` usado como metadata, no como lógica.
- [ ] OpenAPI disponible en Development.
- [ ] Pruebas de Postman completadas.
- [ ] Build final sin errores.

## Referencias oficiales

- ASP.NET Core Web API Controllers: <https://learn.microsoft.com/aspnet/core/web-api/?view=aspnetcore-10.0>
- Model binding: <https://learn.microsoft.com/aspnet/core/mvc/models/model-binding?view=aspnetcore-10.0>
- Validación MVC: <https://learn.microsoft.com/aspnet/core/mvc/models/validation?view=aspnetcore-10.0>
- Manejo de errores en APIs: <https://learn.microsoft.com/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0>
- OpenAPI en ASP.NET Core: <https://learn.microsoft.com/aspnet/core/fundamentals/openapi/overview?view=aspnetcore-10.0>
- Diagnósticos de `IExceptionHandler` en .NET 10: <https://learn.microsoft.com/dotnet/core/compatibility/aspnet-core/10/exception-handler-diagnostics-suppressed>

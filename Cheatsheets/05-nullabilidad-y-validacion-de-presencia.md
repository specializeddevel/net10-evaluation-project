# Cheat sheet 05: Nullabilidad y validación de presencia

## Alcance

Proyecto: `CustomerService.Api`, .NET 10 / C# 14.

El bloque distingue ausencia, valor predeterminado y valor inválido. Incluye anotaciones de referencias anulables, `Nullable<T>`, operadores, pattern matching y el laboratorio de `BirthDate`.

| Archivo | Uso en el bloque |
|---|---|
| `src/CustomerService.Api/CustomerService.Api.csproj` | `<Nullable>enable</Nullable>` |
| `src/CustomerService.Api/Models/Customer.cs` | `DateTimeOffset? UpdatedAt` |
| `src/CustomerService.Api/Services/ICustomerService.cs` | `Customer? GetById(long id)` |
| `src/CustomerService.Api/Services/InMemoryCustomerService.cs` | `long? excludedCustomerId`, cortocircuito y guard clauses |
| `src/CustomerService.Api/Controllers/CustomersController.cs` | Comprobación de ausencia y respuesta `404` |
| `src/CustomerService.Api/Contracts/CreateCustomerRequest.cs` | Record posicional con `IValidatableObject` |
| `src/CustomerService.Api/Contracts/UpdateCustomerRequest.cs` | Propiedades `required` y `IValidatableObject` |
| `src/CustomerService.Api/Program.cs` | Presencia de parámetros del constructor en JSON |

## Referencias anulables frente a tipos valor anulables

`Customer?` sigue siendo una referencia a `Customer`. El `?` comunica al compilador que puede ser `null`; no crea un envoltorio ni impide por sí mismo un `null` en ejecución. `<Nullable>enable</Nullable>` activa las anotaciones y advertencias.

`long?` es `Nullable<long>`. Puede contener cualquier `long`, incluido `0`, o estar vacío. `DateTimeOffset?` funciona igual, pero con fechas. Una referencia como `Customer?` no tiene los miembros `HasValue` y `Value` de `Nullable<T>`.

| Declaración | Significado |
|---|---|
| `Customer` | El código espera una referencia no nula |
| `Customer?` | Se admite una referencia nula |
| `long` | Siempre contiene un número |
| `long?` | Número o ausencia; `0` y `null` son diferentes |

Equivalencias aproximadas con Java: una referencia anulable más análisis estático se aproxima a anotaciones de nullabilidad; `?? throw` recuerda a `Optional.orElseThrow()`. C# no obliga a usar un `Optional<T>` para expresar una referencia posiblemente nula.

## Análisis de flujo

Fragmento del controller, archivo `src/CustomerService.Api/Controllers/CustomersController.cs`:

```csharp
var customer = _customerService.GetById(id);

if (customer is null)
{
    return NotFound();
}

return Ok(customer.ToResponse());
```

Después del `if`, el compilador sabe que `customer` no es nulo: el camino donde faltaba terminó con `return`. Las advertencias ayudan a descubrir accesos inseguros; por defecto son advertencias, aunque un proyecto puede tratarlas como errores.

## Operadores

Ejemplos conceptuales aplicables al controller o servicio; no son cambios adicionales del laboratorio:

| Operador | Ejemplo | Resultado |
|---|---|---|
| `?.` | `customer?.Email` | Lee el miembro si existe el objeto; en caso contrario devuelve `null` |
| `??` | `customer?.Email ?? "No disponible"` | Usa el segundo operando solo cuando el primero es `null` |
| `!` | `customer!.Email` | Silencia la advertencia; si el objeto es nulo, el acceso sigue fallando |
| `?? throw` | `value ?? throw new InvalidOperationException()` | Devuelve el valor o lanza cuando es nulo |

Un fallback debe tener significado. Para una fecha de última actividad puede tener sentido `customer.UpdatedAt ?? customer.CreatedAt`. Para un cliente inexistente en nuestro CRUD, conservamos el `404`; una excepción inesperada acabaría en `500` con el handler actual.

## `Nullable<T>`: leer el valor

| Miembro de `long?` | Si contiene `7` | Si está vacío |
|---|---|---|
| `HasValue` | `true` | `false` |
| `Value` | `7` | Lanza `InvalidOperationException` |
| `GetValueOrDefault()` | `7` | `0` |

`GetValueOrDefault()` no lanza por estar vacío. Devuelve `default(T)`, que puede ocultar la ausencia: para una fecha produciría una fecha predeterminada, no una fecha de negocio elegida deliberadamente.

## Cortocircuito en `EnsureUnique`

Archivo: `src/CustomerService.Api/Services/InMemoryCustomerService.cs`.

```csharp
!excludedCustomerId.HasValue
    || customer.Id != excludedCustomerId.Value
```

Durante create, `excludedCustomerId` es `null`: la primera parte es `true` y `||` no evalúa `.Value`. Durante update, la primera parte es `false`, por lo que existe un valor y se compara con el ID del cliente examinado. El propio cliente queda excluido; los demás siguen participando en la búsqueda de duplicados.

El orden de las comprobaciones hace seguro el acceso. `||` se detiene cuando la izquierda es verdadera; `&&` se detiene cuando la izquierda es falsa.

## Pattern matching

Ejemplo conceptual para el servicio:

```csharp
if (excludedCustomerId is long idToExclude)
{
    // idToExclude es long y contiene un valor.
}
```

Es equivalente en intención a comprobar `HasValue` y extraer `Value`. La condición de unicidad también podría expresarse así:

```csharp
excludedCustomerId is not long idToExclude
    || customer.Id != idToExclude
```

Si la izquierda es falsa, el compilador sabe que `idToExclude` está asignado. Son alternativas conceptuales: el código con `HasValue` sigue siendo válido y claro.

## Nullabilidad, `required`, `[Required]` y JSON

| Mecanismo | Qué garantiza |
|---|---|
| `string?` | El contrato del código admite `null` |
| `string` | El contrato estático espera una referencia no nula |
| `required` | El llamador de C# debe inicializar el miembro; System.Text.Json exige su presencia en JSON |
| `[Required]` | Validación de valor: rechaza `null` y, para strings con configuración predeterminada, vacío o espacios |
| `RespectRequiredConstructorParameters` | Exige en JSON los parámetros no opcionales del constructor |

`required` permite asignar `null`, aunque un tipo referencia no anulable genera advertencia. Tampoco rechaza un valor predeterminado explícito como `DateOnly.MinValue`.

MVC suele inferir una validación equivalente a `[Required(AllowEmptyStrings = true)]` sobre referencias no anulables. Es un comportamiento del framework, configurable, distinto de la garantía del lenguaje. Nuestros DTO usan atributos explícitos y mensajes propios. No hay una garantía general de que toda referencia no anulable jamás reciba `null`.

## Pregunta del curso: ¿`DateOnly?` permite omitir el campo?

Con `RespectRequiredConstructorParameters = true`, sin `[Required]` adicional:

| Parámetro del record posicional | Omisión del campo | JSON con `null` |
|---|---|---|
| `DateOnly BirthDate` | Error | Error de conversión |
| `DateOnly? BirthDate` | Error | Permitido por el serializador |
| `DateOnly? BirthDate = null` | Permitida; queda `null` | Permitido por el serializador |

El `?` permite un valor nulo; el valor predeterminado del parámetro lo vuelve opcional. Son decisiones independientes. Si cambiáramos el DTO a `DateOnly?`, también habría que adaptar las reglas y el mapeo que hoy esperan `DateOnly`.

## Laboratorio: fecha ausente

Inicialmente, el POST sin `birthDate` devolvió `201` con `"birthDate": "0001-01-01"`. El serializador usó el valor predeterminado del parámetro y la regla de edad mínima lo aceptó.

Cambio incorporado en `src/CustomerService.Api/Program.cs`:

```csharp
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.RespectRequiredConstructorParameters = true;
    });
```

Esto afecta los formatters JSON de MVC. No requiere un paquete nuevo. Los parámetros del constructor sin valor predeterminado deben estar presentes; no define qué fechas son válidas.

El usuario verificó después un `400`: el error en `$` decía que faltaba `birthDate`; el error adicional en `request` era consecuencia de no poder construir el parámetro requerido de la acción.

## Laboratorio: fecha predeterminada explícita

Con la opción anterior, enviar `"birthDate": "0001-01-01"` todavía devolvió `201`, resultado confirmado por el usuario. El campo estaba presente y era convertible a `DateOnly`.

Ambos DTO actuales implementan `IValidatableObject`. Método incorporado en `src/CustomerService.Api/Contracts/CreateCustomerRequest.cs` y `src/CustomerService.Api/Contracts/UpdateCustomerRequest.cs`:

```csharp
public IEnumerable<ValidationResult> Validate(
    ValidationContext validationContext)
{
    if (BirthDate == DateOnly.MinValue)
    {
        yield return new ValidationResult(
            "Birth date must not be the default date.",
            [nameof(BirthDate)]);
    }
}
```

`yield return` entrega un resultado de validación cuando la condición falla. `nameof(BirthDate)` asocia el error al miembro. MVC ejecuta la validación y `[ApiController]` puede responder `400` antes de la acción. El rechazo de `DateOnly.MinValue` no define un rango completo de fechas de nacimiento plausibles; es la regla concreta de este laboratorio.

La edad mínima sigue en el servicio y produce `422`. La validación del DTO no protege por sí sola las llamadas directas al servicio; las invariantes del dominio deberán conservar protección cuando evolucionemos la arquitectura. La regla repetida entre DTO podría extraerse después a un atributo reutilizable.

## Guard clauses y excepciones en una línea

Archivo: `src/CustomerService.Api/Services/InMemoryCustomerService.cs`.

```csharp
ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 100);
```

Son métodos estáticos que comprueban una condición y lanzan el tipo de excepción indicado si falla. Los números son límites de comparación, no códigos HTTP. Si la condición no se cumple, la ejecución continúa. El nombre del argumento puede inferirse automáticamente o especificarse como tercer argumento.

Equivalente conceptual de la primera comprobación:

```csharp
if (page < 1)
{
    throw new ArgumentOutOfRangeException(
        nameof(page), page, "Page must be at least 1.");
}
```

El helper genera su propio mensaje. No todas las excepciones ofrecen helpers `ThrowIf...`; para una propia se puede usar `throw new MiExcepcion(...)`.

En nuestro endpoint, MVC rechaza `page=0` con `400` mediante `[Range]` antes de llegar al servicio. Las guard clauses protegen también llamadas internas. Si una `ArgumentOutOfRangeException` llega al handler global actual, se considera inesperada y da `500`: la excepción no elige automáticamente un status HTTP.

## Comandos y pruebas en Postman

Directorio: `C:\Users\RaulBurgos\source\repos\dotnet-course`.

```powershell
dotnet build .\CustomerService.slnx
dotnet run --project .\src\CustomerService.Api\CustomerService.Api.csproj --launch-profile https
```

`build` debe terminar sin errores. `run` inicia la API con el perfil HTTPS del curso. Reiniciar después de editar DTOs o configuración. Si el puerto está ocupado, detener la instancia anterior; si sigue el comportamiento antiguo, comprobar que se ejecuta el proceso recompilado.

POST y PUT usan headers `Content-Type: application/json`, `Accept: application/json`, Body → raw → JSON. Cuerpo de referencia:

```json
{
  "firstName": "Prueba",
  "lastName": "Nullabilidad",
  "email": "nullabilidad-prueba@example.com",
  "documentNumber": "NULL-TEST-001",
  "birthDate": "1990-01-01"
}
```

| Prueba | Método y URL | Body / resultado esperado |
|---|---|---|
| Campo ausente | POST `https://localhost:7013/api/customers` | Omitir `birthDate`; `400`, error de deserialización |
| Null explícito | POST `https://localhost:7013/api/customers` | `birthDate: null`; `400`, error de conversión a `DateOnly` |
| Valor predeterminado | POST `https://localhost:7013/api/customers` | `birthDate: "0001-01-01"`; `400`, error de `BirthDate` |
| Fecha válida | POST `https://localhost:7013/api/customers` | Cuerpo de referencia con email/documento únicos; `201`, fecha preservada |
| Update inválido | PUT `https://localhost:7013/api/customers/1` | Usar los datos actuales del cliente 1 y fecha predeterminada; `400`, no modifica el cliente |
| Estado tras update rechazado | GET `https://localhost:7013/api/customers/1` | Header `Accept: application/json`, sin body; `200`, fecha anterior intacta |

Un email o documento repetido en la prueba válida puede producir `409`. Las pruebas iniciales que dieron `201` crearon clientes en memoria; reiniciar la API reconstruye la lista inicial. Los resultados esperados de la tabla no sustituyen la ejecución de cada prueba.

## Historia y diferencias de versiones

| Característica | Primera versión de C# | Contexto habitual de lanzamiento |
|---|---|---|
| `Nullable<T>`, `int?`, `??` | C# 2 (2005) | .NET Framework 2.0 |
| `?.` | C# 6 (2015) | .NET Framework 4.6 |
| Declaration patterns, `is null`, `?? throw` | C# 7 (2017) | Época de .NET Framework 4.7 |
| Referencias anulables, `!`, `??=`, patrones de propiedades | C# 8 (2019) | .NET Core 3.0 |
| Patrones `not`, `and`, `or`, relacionales | C# 9 (2020) | .NET 5 |
| Miembros `required` | C# 11 (2022) | .NET 7 |

La sintaxis depende del compilador C#; no todas las características requieren el runtime asociado históricamente. `RespectRequiredConstructorParameters` está disponible desde .NET 9. Los helpers de comparación `ThrowIfLessThan` y `ThrowIfGreaterThan` están disponibles desde .NET 8. Nuestro baseline es .NET 10 / C# 14.

## Correcciones de la evaluación

- `customer!.Email` puede lanzar `NullReferenceException`; `!` no comprueba ni asigna un valor.
- `Customer?` es una referencia anotada; `long?` es un `Nullable<long>` con estados vacío o con valor.
- El cortocircuito protege `.Value` en la condición de `EnsureUnique`.
- `DateOnly? BirthDate = null` permite omisión del parámetro del constructor, salvo reglas de validación adicionales.
- `required` no rechaza una fecha predeterminada explícita; `Validate` aporta esa regla.
- `GetValueOrDefault()` devuelve `default(T)` cuando está vacío. `.Value` es el miembro que lanza.
- `?? throw` lanza si el valor de la izquierda es nulo.
- Una excepción de argumento no establece por sí sola `400`.

## Preguntas de entrevista y buenas prácticas

**¿El compilador garantiza que una referencia no anulable jamás sea null?** No. El análisis estático reduce errores; deserialización, código sin anotaciones y otras fronteras necesitan controles apropiados.

**¿Cuándo usar `!`?** Cuando hay una garantía real que el análisis no puede deducir. Antes, preferir contratos correctos y comprobaciones que hagan visible la garantía.

**¿Por qué no sustituir toda ausencia por `default`?** Porque puede convertir un dato faltante en un número o fecha que parezca real, ocultando la intención.

**¿Nullabilidad reemplaza la validación HTTP o de negocio?** No. Describe posibilidades del código; las otras validaciones comprueban el contenido y las reglas del caso de uso.

## Checklist

- [ ] Distingo `Customer?` de `Nullable<long>`.
- [ ] Distingo `null`, `0` y otros valores predeterminados.
- [ ] Accedo a `.Value` solo cuando existe un valor.
- [ ] Uso `?.` y `??` con un significado de negocio claro.
- [ ] Evito silenciar advertencias con `!` sin justificación.
- [ ] Entiendo el análisis de flujo y el cortocircuito.
- [ ] Distingo parámetro nullable de parámetro opcional.
- [ ] Pruebo omisión, null explícito y valor predeterminado en JSON.
- [ ] La fecha predeterminada se valida tanto en create como en update.
- [ ] Distingo guard clauses de status HTTP.

## Referencias oficiales

- [Referencias anulables y análisis estático](https://learn.microsoft.com/dotnet/csharp/fundamentals/null-safety/nullable-reference-types)
- [Operadores de nullabilidad](https://learn.microsoft.com/dotnet/csharp/fundamentals/null-safety/null-operators)
- [Pattern matching](https://learn.microsoft.com/dotnet/csharp/fundamentals/functional/pattern-matching)
- [Miembros required](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/required)
- [Presencia de propiedades y parámetros en System.Text.Json](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/required-properties)
- [Validación de MVC e IValidatableObject](https://learn.microsoft.com/aspnet/core/mvc/models/validation?view=aspnetcore-10.0)
- [ThrowIfLessThan](https://learn.microsoft.com/dotnet/api/system.argumentoutofrangeexception.throwiflessthan?view=net-10.0)
- [Historia de C#](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-version-history)

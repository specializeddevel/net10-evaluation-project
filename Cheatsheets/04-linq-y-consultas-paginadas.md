# Cheat sheet 04: LINQ y consultas paginadas

## Alcance y archivos

Proyecto: `CustomerService.Api` (`net10.0`, C# 14).

Este bloque usa LINQ sobre la lista en memoria del servicio. La traducción a SQL se estudiará con EF Core.

| Archivo | Responsabilidad |
|---|---|
| `src/CustomerService.Api/Contracts/GetCustomersRequest.cs` | Filtro, página, tamaño y dirección del orden; validación del input |
| `src/CustomerService.Api/Contracts/PagedResponse.cs` | `Items`, `TotalCount`, `Page`, `PageSize` y `TotalPages` |
| `src/CustomerService.Api/Services/ICustomerService.cs` | Contrato de `Search` |
| `src/CustomerService.Api/Services/InMemoryCustomerService.cs` | Filtro, conteo, orden y paginación en memoria |
| `src/CustomerService.Api/Controllers/CustomersController.cs` | Binding de query string, mapeo a DTO de salida y respuesta HTTP |

## Mapa rápido Java → C#

| Java | C# / LINQ | Nota |
|---|---|---|
| `stream().filter(...)` | `Where(...)` | Filtra |
| `stream().map(...)` | `Select(...)` | Proyecta |
| `stream().flatMap(...)` | `SelectMany(...)` | Aplana secuencias |
| `findFirst().orElse(null)` | `FirstOrDefault(...)` | Primera coincidencia o valor predeterminado |
| `anyMatch(...)` | `Any(...)` | Alguna coincidencia |
| `allMatch(...)` | `All(...)` | Todos cumplen |
| `distinct()` | `Distinct()` | Depende de la igualdad del elemento |
| `sorted(...)` | `OrderBy(...).ThenBy(...)` | Orden compuesto |
| `skip(n).limit(m)` | `Skip(n).Take(m)` | Página por desplazamiento |
| `collect(toList())` | `ToList()` | Materializa |

Son equivalencias aproximadas: `IQueryable<T>` y un proveedor como EF Core agregan la posibilidad de traducir una consulta a SQL.

## Componer, ejecutar y materializar

```csharp
IEnumerable<Customer> query = customers.Where(c => c.LastName == "Burgos");
```

`Where` construye una secuencia diferida. La condición se evalúa al recorrerla, por ejemplo con `foreach`, `Count()` o `ToArray()`. `Select` también suele ser diferido; `OrderBy` se ejecuta durante la enumeración y debe reunir los elementos necesarios para ordenarlos.

```csharp
int count = query.Count();      // Primer recorrido.
Customer[] array = query.ToArray(); // Segundo recorrido.
```

`query` no guarda automáticamente el resultado del primer recorrido. Si la fuente cambia entre ambos, los resultados pueden diferir. Materializar con `ToArray()` o `ToList()` conserva una instantánea de las referencias devueltas en ese momento; no vuelve inmutables a los objetos.

En el `Search` actual, `Count()` recorre la secuencia filtrada y `ToArray()` puede recorrerla otra vez para obtener la página. Si el desplazamiento ya está fuera del total, el servicio devuelve `[]` sin ese segundo recorrido.

## Operadores de uso diario

| Necesidad | Operador | Precaución |
|---|---|---|
| Filtrar | `Where` | Devuelve una secuencia; no ejecuta de inmediato |
| Proyectar | `Select` | Puede cambiar el tipo de salida |
| Aplanar colecciones internas | `SelectMany` | Multiplica filas si cada elemento tiene varios hijos |
| Existe alguno | `Any` | Se detiene en la primera coincidencia |
| Todos cumplen | `All` | Devuelve `true` en una secuencia vacía |
| Un valor pertenece a la colección | `Contains` | Usa las reglas de igualdad del tipo |
| Contar | `Count` / `LongCount` | Ejecutan la consulta; `LongCount` devuelve `long` |
| Primera coincidencia | `FirstOrDefault` | No comprueba unicidad |
| Como máximo una | `SingleOrDefault` | Lanza excepción si hay varias |
| Orden principal y desempate | `OrderBy(...).ThenBy(...)` | Ordenar antes de paginar |
| Página | `Skip(...).Take(...)` | Requiere un orden definido |
| Quitar duplicados | `Distinct` / `DistinctBy` | Igualdad completa o clave seleccionada |

`Contains` tiene dos usos distintos: `selectedIds.Contains(customer.Id)` pregunta por pertenencia a una colección; `customer.Email.Contains(term, StringComparison.OrdinalIgnoreCase)` busca una subcadena en un texto.

## Cardinalidad: `First` y `Single`

| Método | 0 coincidencias | 1 coincidencia | Varias coincidencias |
|---|---|---|---|
| `First` | Excepción | Devuelve elemento | Devuelve primero |
| `FirstOrDefault` | `default` | Devuelve elemento | Devuelve primero |
| `Single` | Excepción | Devuelve elemento | Excepción |
| `SingleOrDefault` | `default` | Devuelve elemento | Excepción |

Para `Customer`, `default` es `null`. Para `int`, es `0`. Usa `FirstOrDefault` si interesa el primero; `SingleOrDefault` si el contrato permite como máximo uno; `Any` si solo interesa saber si existe. `SingleOrDefault` detecta duplicados durante la lectura, pero no sustituye las validaciones al escribir ni un futuro índice único en la base de datos.

`Last` y `LastOrDefault` operan sobre el último elemento de la secuencia. Si lo que buscas es «el más reciente», establece primero el orden que define *reciente*; no dependas del orden accidental de almacenamiento.

## Cuantificadores y secuencias vacías

```csharp
customers.Any();                             // ¿Hay al menos uno?
customers.Any(c => c.Email.Contains("@"));  // ¿Alguno cumple?
customers.All(c => c.Email.Contains("@"));  // ¿Todos cumplen?
```

En una secuencia vacía, `Any()` y `Any(predicate)` devuelven `false`; `All(predicate)` devuelve `true` porque ningún elemento incumple la condición. Para exigir una colección no vacía cuyos elementos cumplan todos:

```csharp
bool valid = customers.Any() && customers.All(c => c.Email.Contains("@"));
```

`Any`, `All` y `Contains` pueden terminar antes de recorrer toda la secuencia. Evita `Count() > 0` cuando solo necesitas existencia.

## Transformaciones, conjuntos y agrupaciones

- `Select` conserva una salida por entrada; `SelectMany` aplana una secuencia de secuencias.
- `Distinct` usa igualdad del objeto. En una clase común, la igualdad predeterminada suele ser por referencia; en un `record`, es por valor. `DistinctBy(c => c.Email)` compara la clave elegida.
- `Concat` agrega secuencias sin eliminar duplicados. `Union` los elimina. `Intersect` conserva elementos comunes y `Except` los que están solo en la primera secuencia. Las variantes `*By` usan una clave.
- `GroupBy` produce grupos al enumerar; `ToLookup` crea una estructura de consulta por clave y admite varias entradas por clave; `ToDictionary` exige claves únicas y lanza excepción ante duplicados.
- `Sum`, `Average`, `Min`, `Max`, `MinBy` y `MaxBy` resumen o eligen elementos. Comprueba el comportamiento para secuencias vacías según operador y tipo: no todos devuelven el mismo valor.

## Relacionar secuencias

`Join` empareja elementos de dos secuencias por una clave, como un `INNER JOIN`. `GroupJoin` conserva cada elemento externo y le asocia un grupo de coincidencias. `SelectMany` puede aplanar esos grupos, construir un producto cartesiano o transformar una relación padre-hijos.

🟣 **.NET 10+**: `LeftJoin` y `RightJoin` proporcionan métodos directos para esos tipos de unión. También puede expresarse un *left join* con `GroupJoin` y `DefaultIfEmpty`. Esta parte fue conceptual: `CustomerService` todavía no tiene pedidos ni una relación real que justifique incorporarla al código.

## `IEnumerable<T>` frente a `IQueryable<T>`

| | `IEnumerable<T>` | `IQueryable<T>` |
|---|---|---|
| Representa | Secuencia recorrible | Consulta que interpreta un proveedor |
| Predicado típico de `Where` | `Func<T, bool>` | `Expression<Func<T, bool>>` |
| En este curso | La lista `_customers` se filtra en el proceso | Más adelante, EF Core podrá traducir operaciones a SQL |
| Limitación | El coste recae en recorrer la fuente | El proveedor debe poder traducir la expresión |

`IEnumerable<T>` no significa necesariamente «todos los datos ya están en memoria»; describe una interfaz recorrible. `IQueryable<T>` tampoco garantiza que cualquier método C# sea traducible por cualquier proveedor.

Ejemplo conceptual para la futura etapa de EF Core:

```csharp
// Recupera todos los clientes y luego filtra en la aplicación.
var customers = await dbContext.Customers.ToListAsync();
var filtered = customers.Where(c => c.LastName == "Burgos").ToArray();

// Permite que EF Core traduzca el filtro antes de recuperar los resultados.
var filteredFromDb = await dbContext.Customers
    .Where(c => c.LastName == "Burgos")
    .ToListAsync();
```

En el primer caso, `ToListAsync()` ya materializó la consulta a la base de datos. El `Where` posterior se ejecuta en la aplicación al hacer `ToArray()`. Cambiar solo ese último método no mueve el filtro a SQL: hay que colocar `Where` antes de materializar.

## Laboratorio real: `GET /api/customers`

Entrada de `GetCustomersRequest`:

| Parámetro de query | Tipo | Valor predeterminado | Validación |
|---|---|---|---|
| `search` | `string?` | `null` | Hasta 100 caracteres; vacío o espacios equivale a no filtrar |
| `page` | `int` | `1` | Desde 1 |
| `pageSize` | `int` | `10` | Entre 1 y 100 |
| `sortDescending` | `bool` | `false` | Debe poder convertirse a booleano |

`[FromQuery] GetCustomersRequest request` indica a MVC que lea esos valores de la URL. `[ApiController]` produce un `400` ante errores de binding o validación antes de ejecutar la acción. El servicio protege adicionalmente sus argumentos con `ArgumentOutOfRangeException.ThrowIf...` para llamadas fuera de HTTP.

Flujo del servicio:

```text
_customers
  → filtro opcional por FirstName, LastName o Email
  → Count(): TotalCount de todas las coincidencias
  → OrderBy/ThenBy: LastName, FirstName, Id
  → Skip(offset).Take(pageSize)
  → ToArray(): Items de la página
```

El filtro de texto usa `Contains(term, StringComparison.OrdinalIgnoreCase)` después de `Trim()`. El ID al final del orden resuelve empates de apellido y nombre. Si se pide una página fuera de rango, `Items` es `[]` y `TotalCount` conserva el total filtrado.

El desplazamiento se calcula como `long`:

```csharp
long offset = ((long)page - 1) * pageSize;
```

Así se evita un desbordamiento en el producto de enteros. El servicio convierte a `int` solo cuando `offset < totalCount`.

La salida del controller es `PagedResponse<CustomerResponse>`: mapea únicamente los modelos incluidos en la página. `TotalPages` se calcula como `Ceiling(TotalCount / PageSize)`; con cero coincidencias vale 0.

## Pruebas en Postman

Directorio para ejecutar la aplicación: `C:\Users\RaulBurgos\source\repos\dotnet-course`.

```powershell
dotnet build .\CustomerService.slnx
dotnet run --project .\src\CustomerService.Api\CustomerService.Api.csproj --launch-profile https
```

`build` compila y debe terminar sin errores; `run` inicia la API en `https://localhost:7013` con el perfil usado en el curso. Si el puerto está ocupado, detén la instancia anterior. Si el build falla, comprueba que el método `Search` coincida entre interfaz e implementación y que los dos contratos existan en `Contracts/`.

Para todas las pruebas: método `GET`, header `Accept: application/json`, sin body (`Body: none`). URL base: `https://localhost:7013/api/customers`.

| Nombre | URL completa | Status y detalle esperado |
|---|---|---|
| Predeterminados | `https://localhost:7013/api/customers` | `200`; `page: 1`, `pageSize: 10` |
| Búsqueda | `https://localhost:7013/api/customers?search=ANA` | `200`; Ana coincide sin distinguir mayúsculas |
| Primera página | `https://localhost:7013/api/customers?page=1&pageSize=1` | `200`; como máximo un elemento; total sin paginar |
| Segunda página | `https://localhost:7013/api/customers?page=2&pageSize=1` | `200`; siguiente elemento del mismo orden |
| Descendente | `https://localhost:7013/api/customers?pageSize=1&sortDescending=true` | `200`; primer elemento del orden descendente |
| Sin coincidencias | `https://localhost:7013/api/customers?search=zzzz-no-existe` | `200`; `items: []`, `totalCount: 0`, `totalPages: 0` |
| Página fuera de rango | `https://localhost:7013/api/customers?page=999&pageSize=10` | `200`; `items: []`, total filtrado intacto |
| Página inválida | `https://localhost:7013/api/customers?page=0` | `400`; error de validación de `Page` |
| Tamaño inválido | `https://localhost:7013/api/customers?pageSize=101` | `400`; error de validación de `PageSize` |
| Tipo inválido | `https://localhost:7013/api/customers?page=abc` | `400`; error de model binding |

Forma aproximada de una respuesta correcta:

```json
{
  "items": [{ "id": 1, "firstName": "Raúl", "lastName": "Burgos" }],
  "totalCount": 2,
  "page": 1,
  "pageSize": 1,
  "totalPages": 2
}
```

Cada elemento real contiene además las otras propiedades de `CustomerResponse`. `TotalCount` y `TotalPages` deben mantenerse al cambiar solo `page` con el mismo filtro.

## Límites y decisiones de producción

- El endpoint pasó de devolver un array a devolver un objeto paginado; es un cambio de contrato para consumidores existentes.
- Este laboratorio usa una `List<Customer>` dentro de un servicio singleton. La lista mutable no es segura frente a escrituras concurrentes; el conteo y la página tampoco forman una instantánea atómica.
- Ordenar antes de paginar da un criterio consistente para datos sin cambios. Si se insertan o borran filas entre solicitudes, la paginación por desplazamiento puede repetir u omitir elementos. Estudiaremos paginación por clave con EF Core.
- Con EF Core, `CountAsync()` y `ToListAsync()` podrían producir consultas separadas; habrá que revisar SQL, traducción, índices y consistencia deseada.
- La comparación de texto con `StringComparison.OrdinalIgnoreCase` describe el comportamiento en memoria. Su equivalente en SQL dependerá del proveedor y de la colación; no asumas traducción idéntica.

## Errores frecuentes y respuestas de repaso

**¿`All` sobre una secuencia vacía devuelve `false`?** No. Devuelve `true`; combina `Any() && All(...)` cuando necesitas al menos un elemento.

**¿`Count()` seguido de `ToArray()` recorre la secuencia una sola vez?** No, una secuencia diferida puede evaluarse de nuevo en cada enumeración. En nuestro servicio hay un segundo recorrido cuando la página está dentro del rango.

**¿Cambiar `ToArray()` por `ToListAsync()` después de `ToListAsync()` mueve el filtro a SQL?** No. La primera materialización ya recuperó los datos. Pon el filtro sobre `dbContext.Customers` antes de materializar.

**¿Por qué contar antes de `Skip` y `Take`?** Porque `TotalCount` describe todas las filas que cumplen el filtro, mientras `Items` describe solo la página.

**¿Por qué ordenar antes de paginar?** Porque la página necesita un orden definido para decidir qué elementos corresponden a ese desplazamiento. Agrega un desempate único como `Id`.

**¿`IQueryable<T>` traduce cualquier código C# a SQL?** No. La traducción depende del proveedor y de la expresión utilizada.

## Diferencias .NET 8 / 9 / 10

Los operadores principales de este laboratorio (`Where`, `Select`, `OrderBy`, `Skip`, `Take`, `Count`) también están disponibles en .NET 8 y 9. 🟣 `LeftJoin` y `RightJoin` como operadores LINQ directos llegan en .NET 10. Las diferencias de SQL al usar EF Core se verán en el bloque de persistencia.

## Preguntas de entrevista

**¿Qué es ejecución diferida?** Construir una consulta sin recorrerla hasta que se enumera o se llama un operador terminal.

**¿Qué diferencia hay entre `FirstOrDefault` y `SingleOrDefault`?** El primero acepta múltiples coincidencias y devuelve la primera; el segundo exige como máximo una.

**¿Por qué `IQueryable<T>` puede producir SQL?** Porque el proveedor recibe una expresión que describe la consulta y puede traducir las operaciones compatibles.

**¿Por qué un endpoint paginado devuelve `TotalCount`?** Para informar el total filtrado, independientemente del tamaño de la página actual.

## Checklist

- [ ] Distingo proyección, filtrado, agrupación, conjuntos y aplanamiento.
- [ ] Elijo `Any`, `All`, `FirstOrDefault` o `SingleOrDefault` según el contrato.
- [ ] Recuerdo que `All` sobre una secuencia vacía es `true`.
- [ ] Distingo composición diferida, ejecución y materialización.
- [ ] Reconozco que una secuencia puede recorrerse más de una vez.
- [ ] Distingo LINQ en memoria de una consulta traducida por EF Core.
- [ ] Cuento las coincidencias antes de paginar.
- [ ] Ordeno con desempate antes de `Skip` y `Take`.
- [ ] Pruebo filtro, páginas, orden y entradas inválidas en Postman.
- [ ] Reviso el efecto del cambio de contrato sobre consumidores.

## Referencias oficiales

- [Introducción a LINQ y ejecución diferida](https://learn.microsoft.com/dotnet/csharp/linq/get-started/introduction-to-linq-queries)
- [Consultas LINQ con sintaxis de métodos](https://learn.microsoft.com/dotnet/csharp/linq/get-started/write-linq-queries)
- [LINQ: `LeftJoin` y `RightJoin` en .NET 10](https://learn.microsoft.com/dotnet/csharp/linq/perform-left-outer-joins)
- [Consultas con EF Core](https://learn.microsoft.com/ef/core/querying/)

# Cheat sheet 06: async/await, Task y cancelación

## Alcance

Proyecto: `CustomerService.Api`, .NET 10 y C# 14.

Archivos del laboratorio:

```text
src/CustomerService.Api/
├── Controllers/AsyncLabController.cs
├── Services/IAsyncLabService.cs
├── Services/AsyncLabService.cs
└── Program.cs
```

Este bloque cubre `Task`, `Task<T>`, `async`, `await`, cancelación, timeouts, concurrencia de operaciones asíncronas, streams asíncronos y `ValueTask<T>`. El laboratorio usa `Task.Delay` para simular I/O. El CRUD en memoria sigue siendo síncrono porque no espera I/O.

## Mapa Java / Spring → .NET

| Java / Spring | .NET | Nota |
|---|---|---|
| `CompletableFuture<T>` | `Task<T>` | Equivalencia aproximada |
| `CompletableFuture<Void>` | `Task` | Finalización sin resultado |
| `future.get()` | `.Result` / `.Wait()` | Espera bloqueante |
| Composición de futuros | `await` | Espera integrada en el lenguaje |
| `CompletableFuture.allOf` | `Task.WhenAll` | Con tipos homogéneos devuelve `T[]` |
| `CompletableFuture.anyOf` | `Task.WhenAny` | Devuelve la tarea ganadora |
| Señal cooperativa | `CancellationToken` | No interrumpe un hilo por la fuerza |
| `Flux<T>` | Sin equivalente exacto; `IAsyncEnumerable<T>` cubre algunos usos | No implementa Reactive Streams |

## Modelo mental

```text
Task<T> representa una operación
             ↓ await
si sigue pendiente, el método puede suspenderse
             ↓
el hilo queda disponible para otro trabajo
             ↓
la operación completa, falla o se cancela
             ↓
el método continúa y obtiene T o propaga el estado terminal
```

Una `Task` no es un hilo. `async` tampoco crea automáticamente otro hilo. Una tarea puede representar I/O, un temporizador, trabajo del thread pool o una operación ya completada.

Un método `async` comienza ejecutándose de forma síncrona. Si encuentra un `await` pendiente, devuelve el control al llamador y continúa más adelante. En ASP.NET Core no se debe depender de continuar en el mismo hilo.

## Task, Task<T> y nullabilidad

| Tipo | Significado |
|---|---|
| `Task` | Operación sin resultado de negocio |
| `Task<string>` | Operación que entrega un string |
| `Task<Customer?>` | Operación que puede entregar un cliente o null |
| `IAsyncEnumerable<Customer>` | Secuencia con elementos obtenidos asíncronamente |

En `Task<Customer?>` hay dos dimensiones:

```text
Task       → cuándo y cómo termina la operación
Customer?  → si existe el cliente al completar correctamente
```

Una tarea puede estar pendiente, completada, fallida o cancelada. No representa un hilo dedicado.

## Declarar y consumir un método async

Implementación actual:

```csharp
public async Task<string> GetMessageAsync(
    CancellationToken cancellationToken)
{
    await Task.Delay(20000, cancellationToken);
    return "Operación completada";
}
```

Aunque la firma devuelve `Task<string>`, dentro se devuelve un `string`. El compilador completa la tarea con ese resultado.

Consumo:

```csharp
Task<string> operation =
    _asyncLabService.GetMessageAsync(cancellationToken);

string message = await operation;
```

La llamada obtiene la tarea. `await` obtiene el resultado cuando termina y propaga fallos o cancelación. Esperar nuevamente una tarea ya completada no reinicia la operación.

Los métodos asíncronos suelen terminar en `Async`. Las interfaces declaran `Task<T>`, pero no llevan `async`: la implementación decide cómo producir la tarea.

## await frente a Result y Wait

| Necesidad | Asíncrono | Bloqueante |
|---|---|---|
| Obtener resultado | `T value = await task;` | `T value = task.Result;` |
| Esperar sin resultado | `await task;` | `task.Wait();` |

`.Result` y `.Wait()` pertenecen al modelo de tareas, pero bloquean el hilo. En ASP.NET Core eso reduce capacidad y puede contribuir al agotamiento del thread pool. La regla práctica para I/O es propagar asincronía entre las capas.

ASP.NET Core no tiene el `SynchronizationContext` clásico de ASP.NET antiguo, por lo que no se debe afirmar que `.Result` siempre produce deadlock. Sigue siendo una mala elección habitual para I/O en una API.

Evitar `async void` salvo handlers de eventos que exijan esa firma: el llamador no puede esperar su finalización ni observar errores de la forma normal.

## I/O, CPU y Task.Run

| Operación | Enfoque |
|---|---|
| Consulta SQL Server | EF Core asíncrono + `await` |
| Llamada HTTP | `HttpClient` asíncrono + `await` |
| Espera simulada | `Task.Delay` + `await` |
| Filtro pequeño en memoria | Código síncrono |
| Cálculo breve | Código síncrono |
| Trabajo pesado prolongado | Evaluar cola, background worker o servicio dedicado |

Para I/O se espera directamente la API asíncrona. Envolverla en `Task.Run` no transforma una API bloqueante en I/O no bloqueante.

`Task.Run` usa el thread pool. En una UI puede evitar bloquear el hilo de interfaz durante CPU intensiva. En ASP.NET Core, mover CPU de un hilo del pool a otro normalmente no mejora la capacidad global.

## Propagación de CancellationToken

Flujo implementado:

```text
HttpContext.RequestAborted
      ↓ CancellationToken
AsyncLabController
      ↓ mismo token
IAsyncLabService
      ↓ mismo token
AsyncLabService
      ↓
Task.Delay
```

ASP.NET Core proporciona el token de la acción a partir de `RequestAborted`. No procede del body, query string o header. Se activa cuando el servidor detecta que la solicitud fue abortada.

La cancelación es cooperativa: el origen la solicita y cada operación debe observar el token. No mata un hilo ni revierte los efectos realizados.

Código propio puede comprobarlo así:

```csharp
cancellationToken.ThrowIfCancellationRequested();
```

Si está cancelado, lanza `OperationCanceledException`. Las APIs que reciben el token, como `Task.Delay`, hacen su propia comprobación.

## Cancelación, trazabilidad y transacción

| Mecanismo | Propósito |
|---|---|
| `CancellationToken` | Comunicar una solicitud de cancelación |
| `traceId` / `Activity` | Correlacionar el recorrido de una operación |
| `RequestId` | Identificar una solicitud HTTP en el servidor |
| Transacción de base de datos | Controlar atomicidad y confirmación |

El token no identifica la transacción ni sirve para correlacionar logs. Cancelar después de producir efectos no los deshace automáticamente.

En el laboratorio, los logs `started` y `canceled` con el mismo `RequestId` confirmaron que el servidor canceló la espera. Postman conservó visible el body de una respuesta anterior: al abandonar una solicitud no siempre existe un cliente esperando una nueva respuesta HTTP. Los logs correlacionados son la evidencia correcta.

## Task.WhenAll

Endpoint actual:

```csharp
Task<string> first =
    _asyncLabService.GetMessageAsync(cancellationToken);

Task<string> second =
    _asyncLabService.GetMessageAsync(cancellationToken);

string[] messages = await Task.WhenAll(first, second);
```

Las llamadas crean e inician las operaciones antes del `await`. Dos esperas independientes de 20 segundos se solapan y tardan aproximadamente 20 segundos. `WhenAll` no inicia las tareas.

Si los resultados tienen el mismo tipo, devuelve `T[]` en el orden recibido. También puede esperar tareas heterogéneas:

```csharp
Task<string> messageTask = GetMessageAsync(token);
Task<int> countTask = GetCountAsync(token);

await Task.WhenAll(messageTask, countTask);

string message = await messageTask;
int count = await countTask;
```

Los últimos `await` no repiten las operaciones.

| Tareas | Estado de WhenAll |
|---|---|
| Todas completan | Completado |
| Alguna falla | Fallido |
| Ninguna falla y alguna se cancela | Cancelado |

`WhenAll` espera a que todas terminen. Una tarea fallida no cancela automáticamente las restantes. Si varias fallan, la tarea conjunta conserva sus excepciones; registrar todas requiere inspeccionarla con cuidado.

Usar concurrencia solo para operaciones independientes. No ejecutar operaciones concurrentes sobre una misma instancia de `DbContext`.

## Task.WhenAny

```csharp
Task<string> completed =
    await Task.WhenAny(first, second);

string result = await completed;
```

El primer `await` identifica qué tarea terminó primero. El segundo obtiene su resultado o propaga su fallo/cancelación. La ganadora no tiene que haber terminado correctamente.

`WhenAny` no cancela ni espera automáticamente las tareas restantes. Si deben detenerse, hay que solicitar cancelación y observar su finalización.

## Timeouts: WaitAsync frente a CancelAfter

`WaitAsync` limita la espera del llamador:

```csharp
string message = await operation.WaitAsync(
    TimeSpan.FromSeconds(2),
    cancellationToken);
```

Si vence el plazo, lanza `TimeoutException`. La tarea original puede continuar porque dejar de esperarla no solicita cancelación.

El endpoint `timeout` solicita que el trabajo se detenga:

```csharp
using var timeoutSource =
    CancellationTokenSource.CreateLinkedTokenSource(
        cancellationToken);

timeoutSource.CancelAfter(TimeSpan.FromSeconds(2));

string message = await _asyncLabService
    .GetMessageAsync(timeoutSource.Token);
```

La fuente enlazada se cancela si el cliente abandona o vence el plazo. Funciona porque el servicio propaga el token a `Task.Delay`. Si una operación ignora el token, puede continuar.

El orden de estos filtros importa:

```csharp
catch (OperationCanceledException)
    when (cancellationToken.IsCancellationRequested)
{
    // Canceló el cliente.
    throw;
}
catch (OperationCanceledException)
    when (timeoutSource.IsCancellationRequested)
{
    // Venció el timeout propio.
    return Problem(statusCode: 504, ...);
}
```

El token enlazado también se cancela cuando se cancela el original; el primer filtro preserva la causa correcta.

El laboratorio usa `504 Gateway Timeout` para simular una dependencia lenta. El status real depende del papel del endpoint y de la existencia de una dependencia upstream.

## IAsyncEnumerable<T>

Implementación actual:

```csharp
public async IAsyncEnumerable<string> StreamMessagesAsync(
    [EnumeratorCancellation]
    CancellationToken cancellationToken)
{
    for (int number = 1; number <= 3; number++)
    {
        await Task.Delay(1000, cancellationToken);
        yield return $"Mensaje {number}";
    }
}
```

`await` espera antes de producir cada elemento. `yield return` entrega uno y conserva el estado para continuar cuando se solicite el siguiente. El trabajo empieza al enumerar.

Consumo general:

```csharp
await foreach (
    string message in stream.WithCancellation(token))
{
    // Procesar el elemento.
}
```

`[EnumeratorCancellation]` permite incorporar el token usado durante la enumeración.

| Retorno | Comportamiento |
|---|---|
| `Task<List<T>>` | Entrega la colección completa al final |
| `IAsyncEnumerable<T>` | Permite procesar elementos progresivamente |

MVC enumera el resultado de `GET /api/labs/async/stream` y lo serializa como JSON. Postman mostró los elementos progresivamente; Scalar presentó el array completo. Transporte, buffering y cliente influyen en la visualización.

Después de enviar headers o parte del body, un fallo ya no puede reemplazar normalmente la respuesta completa con `ProblemDetails`. Con EF Core, el stream puede mantener conexión y reader abiertos durante más tiempo.

`IAsyncEnumerable<T>` no equivale a Reactor `Flux<T>` ni implementa Reactive Streams.

## ValueTask<T>

`ValueTask<T>` puede representar un resultado ya disponible o una operación pendiente. Puede reducir asignaciones cuando un camino crítico completa sincrónicamente con mucha frecuencia y las mediciones justifican optimizarlo.

`Task<T>` es la elección habitual en servicios de aplicación. `ValueTask<T>` agrega restricciones y complejidad; como regla segura, esperarlo una vez. `AsTask()` permite convertirlo cuando se necesita una tarea.

El proyecto ya implementa un contrato del framework que devuelve `ValueTask<bool>`: `IExceptionHandler.TryHandleAsync`. Eso no justifica convertir nuestros servicios.

## Asincronía, concurrencia, paralelismo y reactividad

| Concepto | Describe |
|---|---|
| Asincronía | Esperar sin bloquear el hilo |
| Concurrencia | Operaciones que progresan durante el mismo intervalo |
| Paralelismo | CPU ejecutando trabajo simultáneo |
| Reactividad | Composición de flujos, eventos, operadores y demanda |

`async/await` no convierte la API en un sistema reactivo como Spring WebFlux. En .NET existe Reactive Extensions con `IObservable<T>` como otro modelo.

Java también dispone de NIO, WebFlux y `CompletableFuture`; no todo Java es bloqueante. Los hilos virtuales permiten código secuencial con muchas esperas sin dedicar un hilo físico a cada una, mediante un modelo diferente.

## Registro en DI

Registro actual en `Program.cs`:

```csharp
builder.Services.AddScoped<IAsyncLabService, AsyncLabService>();
```

`Scoped` entrega una instancia por request. Será también el lifetime habitual para servicios que dependan del `DbContext` scoped.

## Endpoints del laboratorio

| Endpoint | Propósito | Tiempo aproximado |
|---|---|---:|
| `GET /api/labs/async/message` | Operación, logs y cancelación | 20 s |
| `GET /api/labs/async/messages` | Dos operaciones con `WhenAll` | 20 s |
| `GET /api/labs/async/timeout` | Token enlazado y plazo | 2 s |
| `GET /api/labs/async/stream` | Tres elementos progresivos | 3 s |

## Comandos

Ejecutar desde:

```text
C:\Users\RaulBurgos\source\repos\dotnet-course
```

Compilar:

```powershell
dotnet build .\CustomerService.slnx
```

Debe finalizar sin errores. Si DI no resuelve `IAsyncLabService`, comprobar el registro antes de `builder.Build()`. Si interfaz e implementación no coinciden, revisar ambas firmas.

Ejecutar:

```powershell
dotnet run --project .\src\CustomerService.Api\CustomerService.Api.csproj --launch-profile https
```

La API debe escuchar en `https://localhost:7013`. Detener la instancia anterior con `Ctrl+C` si el puerto está ocupado.

## Pruebas en Postman o Scalar

Todas usan `Accept: application/json` y sin body.

| Prueba | URL | Resultado esperado |
|---|---|---|
| Normal | `https://localhost:7013/api/labs/async/message` | `200` tras ~20 s; logs `started → completed` con el mismo RequestId |
| Cancelación | misma URL; cancelar antes de 20 s | Logs `started → canceled`; no se garantiza respuesta HTTP |
| Concurrencia | `https://localhost:7013/api/labs/async/messages` | `200` tras ~20 s y dos mensajes |
| Timeout | `https://localhost:7013/api/labs/async/timeout` | `504 ProblemDetails` tras ~2 s |
| Stream | `https://localhost:7013/api/labs/async/stream` | `200` tras ~3 s y mensajes 1, 2 y 3 |

Body de `messages`:

```json
[
  "Operación completada",
  "Operación completada"
]
```

Body final de `stream`:

```json
[
  "Mensaje 1",
  "Mensaje 2",
  "Mensaje 3"
]
```

## Errores frecuentes

- Creer que una tarea es un hilo.
- Creer que `async` crea un hilo automáticamente.
- Usar `.Result` o `.Wait()` dentro del flujo asíncrono.
- Envolver I/O asíncrono en `Task.Run`.
- Convertir trabajo breve en memoria a async sin beneficio.
- Aceptar el token y no propagarlo a servicio, EF Core o `HttpClient`.
- Confundir cancelación con trazabilidad o rollback.
- Suponer que `WhenAll` inicia tareas o cancela las restantes.
- Suponer que `WhenAny` garantiza éxito.
- Suponer que `WaitAsync` detiene la operación original.
- Usar una misma instancia de `DbContext` concurrentemente.
- Suponer que todos los clientes visualizan un stream igual.
- Elegir `ValueTask<T>` sin mediciones.

## Diferencias de versiones

- `Task` existe desde .NET Framework 4.
- `async/await` llegó con C# 5 y .NET Framework 4.5.
- `IAsyncEnumerable<T>` y `await foreach` llegaron con C# 8 / .NET Core 3.0.
- `Task.WaitAsync` está disponible desde .NET 6.
- Las APIs centrales estudiadas se mantienen en .NET 8, 9 y 10.
- El proyecto usa .NET 10 / C# 14; EF Core deberá usar paquetes 10.x.

## Correcciones de la evaluación

- `Task<Customer?>` representa una operación, no un hilo.
- `async` no crea otro hilo automáticamente.
- Operaciones independientes iniciadas antes de `WhenAll` solapan sus esperas.
- Esperar otra vez una tarea completada no la reinicia.
- `WhenAny` no cancela las demás y su ganadora puede fallar o cancelarse.
- `WhenAll` admite tareas con resultados distintos; luego se lee cada tarea tipada.
- `WaitAsync` limita la espera; `CancelAfter` solicita cancelación.
- Una operación que ignora el token puede continuar.
- El token comunica cancelación; `traceId` correlaciona el recorrido.
- Cancelar no revierte efectos.
- `IAsyncEnumerable<T>` no es Reactor `Flux<T>`.
- `Task<T>` es la opción general; `ValueTask<T>` requiere justificación.

## Preguntas de entrevista

### ¿async crea un hilo?

No. Permite usar `await` y que el método se suspenda. La operación esperada determina cómo se ejecuta.

### ¿Qué diferencia hay entre Task y un hilo?

La tarea representa una operación y su estado; el hilo es un recurso de ejecución.

### ¿Por qué evitar Result en ASP.NET Core?

Puede bloquear hilos durante I/O y reducir capacidad. Se prefiere `await`.

### ¿Está garantizada la cancelación?

No. Es cooperativa: la operación debe observar el token.

### ¿WhenAll crea paralelismo?

No por sí mismo. Espera tareas iniciadas. Puede solapar I/O; el paralelismo de CPU depende de su ejecución.

### ¿WaitAsync cancela la tarea original?

No por sí solo; limita la espera del llamador.

### ¿Cuándo usar IAsyncEnumerable<T>?

Cuando conviene procesar elementos conforme llegan sin materializar toda la colección.

### ¿Cuándo usar ValueTask<T>?

Cuando un contrato lo exige o una medición demuestra un beneficio.

## Checklist

- [ ] Distingo `Task`, `Task<T>` e `IAsyncEnumerable<T>`.
- [ ] Sé que una tarea no es un hilo.
- [ ] Uso `await` y evito bloqueo síncrono en la API.
- [ ] Mantengo síncrono el trabajo breve en memoria.
- [ ] Propago `CancellationToken` hasta el I/O.
- [ ] Distingo cancelación, tracing y transacción.
- [ ] Comprendo `WhenAll` y `WhenAny`.
- [ ] Evito concurrencia sobre un mismo `DbContext`.
- [ ] Distingo timeout de espera y cancelación.
- [ ] Comprendo cancelación cooperativa.
- [ ] Reconozco beneficios y límites del streaming.
- [ ] Elijo `Task<T>` por defecto.

## Referencias oficiales

- [Modelo asíncrono basado en tareas](https://learn.microsoft.com/dotnet/csharp/asynchronous-programming/task-asynchronous-programming-model)
- [Escenarios de programación asíncrona](https://learn.microsoft.com/dotnet/csharp/asynchronous-programming/async-scenarios)
- [Cancelación en código administrado](https://learn.microsoft.com/dotnet/standard/threading/cancellation-in-managed-threads)
- [Combinar cancelación y timeout](https://learn.microsoft.com/dotnet/standard/asynchronous-programming-patterns/coalesce-cancellation-tokens-from-timeouts)
- [Secuencias asíncronas](https://learn.microsoft.com/dotnet/csharp/asynchronous-programming/generate-consume-asynchronous-stream)
- [Task.WhenAll](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task.whenall?view=net-10.0)
- [Task.WhenAny](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task.whenany?view=net-10.0)
- [Task.WaitAsync](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task.waitasync?view=net-10.0)
- [HttpContext.RequestAborted](https://learn.microsoft.com/aspnet/core/fundamentals/use-http-context?view=aspnetcore-10.0#requestaborted)


using CustomerService.Api.Contracts;
using CustomerService.Api.Mappings;
using CustomerService.Api.Models;
using CustomerService.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CustomerService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<CustomerResponse>>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status500InternalServerError)]
    public ActionResult<IReadOnlyCollection<CustomerResponse>> GetAll()
    {
        CustomerResponse[] response = _customerService
            .GetAll()
            .Select(customer => customer.ToResponse())
            .ToArray();

        return Ok(response);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType<CustomerResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status500InternalServerError)]
    public ActionResult<CustomerResponse> GetById(long id)
    {
        var customer = _customerService.GetById(id);

        if (customer is null)
        {
            return NotFound();
        }

        return Ok(customer.ToResponse());
    }

    [HttpPost]
    [ProducesResponseType<CustomerResponse>(
        StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status409Conflict)]        
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status500InternalServerError)]
    public ActionResult<CustomerResponse> Create(
        CreateCustomerRequest request)
    {
        Customer customer = _customerService.Create(request);

        CustomerResponse response = customer.ToResponse();

        return CreatedAtAction(
            nameof(GetById),
            new { id = customer.Id },
            response);
    }
    
    [HttpPut("{id:long}")]
    [ProducesResponseType<CustomerResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status500InternalServerError)]
    public ActionResult<CustomerResponse> Update(
        long id,
        UpdateCustomerRequest request)
    {
        Customer? customer = _customerService.Update(id, request);

        if (customer is null)
        {
            return NotFound();
        }

        CustomerResponse response = customer.ToResponse();

        return Ok(response);
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status500InternalServerError)]
    public IActionResult Delete(long id)
    {
        bool deleted = _customerService.Delete(id);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    #if DEBUG
        [HttpGet("simulate-error")]
        [ProducesResponseType<ProblemDetails>(
            StatusCodes.Status500InternalServerError)]
        public IActionResult SimulateUnexpectedError()
        {
            throw new InvalidOperationException(
                "Simulated unexpected error.");
        }
    #endif
}
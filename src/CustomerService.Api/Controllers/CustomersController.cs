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
    [ProducesResponseType<PagedResponse<CustomerResponse>>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedResponse<CustomerResponse>>> GetAll(
        [FromQuery] GetCustomersRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _customerService.SearchAsync(
            request.Search,
            request.Page,
            request.PageSize,
            request.SortDescending,
            cancellationToken);

        CustomerResponse[] items = result.Items
            .Select(customer => customer.ToResponse())
            .ToArray();

        var response = new PagedResponse<CustomerResponse>(
            items,
            result.TotalCount,
            request.Page,
            request.PageSize);

        return Ok(response);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType<CustomerResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CustomerResponse>> GetById(
    long id,
    CancellationToken cancellationToken)
    {
        Customer? customer = await _customerService.GetByIdAsync(
            id,
            cancellationToken);

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
        StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CustomerResponse>> Create(
        CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        Customer customer = await _customerService.CreateAsync(request, cancellationToken);

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
        StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CustomerResponse>> Update(
        long id,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        Customer? customer = await _customerService.UpdateAsync(id, request, cancellationToken);

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
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        bool deleted = await _customerService.DeleteAsync(id, cancellationToken);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

}
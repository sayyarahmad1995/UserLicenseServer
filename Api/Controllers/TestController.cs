using Api.Errors;
using Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace Api.Controllers;

public class TestController : BaseApiController
{
	private readonly ICacheRepository _cacheRepo;
	private readonly IUnitOfWork _unitOfWork;
	private readonly IConnectionMultiplexer _redis;
	public TestController(IUnitOfWork unitOfWork, ICacheRepository cacheRepo, IConnectionMultiplexer redis)
	{
		_redis = redis;
		_unitOfWork = unitOfWork;
		_cacheRepo = cacheRepo;
	}

	[HttpGet("slow")]
	public async Task<IActionResult> Slow(CancellationToken cancellationToken)
	{
		Console.WriteLine("Started slow request");

		try
		{
			// Simulate long-running task
			await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
			return Ok("Finished slow request");
		}
		catch (OperationCanceledException)
		{
			Console.WriteLine("Request was cancelled due to shutdown");
			return StatusCode(499, "Request cancelled due to shutdown");
		}
	}

	[HttpGet("notfound")]
	public IActionResult GetNotFound()
	{
		return NotFound(new ApiResponse(404));
	}

	[HttpGet("validationerror")]
	public IActionResult GetValidationError()
	{
		var errors = new List<string>
		{
			"Name is required",
			"Email must be a valid email address"
		};

		var validationErrorResponse = new ApiValidationErrorResponse
		{
			Errors = errors
		};

		return BadRequest(validationErrorResponse);
	}

	[HttpGet("servererror")]
	public IActionResult GetServerError()
	{
		throw new Exception("This is a server error for testing purposes");
	}

	[HttpGet("badrequest")]
	public IActionResult GetBadRequest()
	{
		return BadRequest(new ApiResponse(400));
	}

	[HttpGet("redis-test")]
	public async Task<IActionResult> GetRedisAsync()
	{
		bool connected = await _cacheRepo.PingAsync();

		if (!connected)
			throw new Exception("Redis not responding");

		return Ok(new
		{
			status = "Connected",
			message = "Redis is healthy"
		});
	}

	[HttpGet("auth-check")]
	[Authorize(Roles = "Admin")]
	public async Task<IActionResult> AuthCheck()
	{
		return Ok(new { message = "You hit the protected endpoint successfully." });
	}

	[HttpGet("claims")]
	public IActionResult Claims()
	{
		return Ok(User.Claims.Select(c => new { c.Type, c.Value }));
	}
}

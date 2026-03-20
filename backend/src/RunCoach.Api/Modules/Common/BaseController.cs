using Microsoft.AspNetCore.Mvc;

namespace RunCoach.Api.Modules.Common;

[ApiController]
[Route("api/v1/[controller]")]
public abstract class BaseController : ControllerBase
{
}

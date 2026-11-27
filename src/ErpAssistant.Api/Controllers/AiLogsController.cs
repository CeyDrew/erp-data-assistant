using ErpAssistant.Application.Abstractions;
using ErpAssistant.Application.Ai;
using ErpAssistant.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpAssistant.Api.Controllers;

[Authorize(Roles = Roles.Admin)]
public sealed class AiLogsController(AiLogService logs) : ApiControllerBase
{
    [HttpGet]
    public Task<PagedResult<AiLogItem>> List([FromQuery] AiLogQuery query, CancellationToken ct) => logs.ListAsync(query, ct);
}

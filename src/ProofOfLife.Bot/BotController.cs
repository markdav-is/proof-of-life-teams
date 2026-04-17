using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;

namespace ProofOfLife.Bot;

[ApiController]
[Route("api/messages")]
public class BotController(IBotFrameworkHttpAdapter adapter, IBot bot) : ControllerBase
{
    [HttpPost]
    public Task PostAsync(CancellationToken ct) =>
        adapter.ProcessAsync(Request, Response, bot, ct);
}

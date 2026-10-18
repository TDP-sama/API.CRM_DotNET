using API.CRM_DotNET.Auth;
using API.CRM_DotNET.Data;
using API.CRM_DotNET.Domain;
using API.CRM_DotNET.Dtos;
using API.CRM_DotNET.Infrastructure;
using DevExtreme.AspNet.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.CRM_DotNET.Controllers;

[ApiController]
[Route("api/opportunities")]
public class OpportunitiesController(CrmDbContext db) : ControllerBase
{
    /// <summary>Win probability used when the client does not send one.</summary>
    public static int DefaultProbability(OpportunityStage stage) => stage switch
    {
        OpportunityStage.New => 10,
        OpportunityStage.Qualified => 25,
        OpportunityStage.Proposal => 50,
        OpportunityStage.Negotiation => 75,
        OpportunityStage.Won => 100,
        _ => 0,
    };

    [HttpGet]
    [Authorize(Policy = Permissions.OpportunitiesView + "|" + Permissions.CustomersView)]
    public async Task<object> Get(DataSourceLoadOptions loadOptions) =>
        await DataSourceLoader.LoadAsync(db.Opportunities.AsNoTracking().Select(o => new OpportunityDto
        {
            Id = o.Id,
            Title = o.Title,
            CustomerId = o.CustomerId,
            CustomerName = o.Customer.Name,
            Stage = o.Stage,
            Value = o.Value,
            Probability = o.Probability,
            WeightedValue = o.Value * o.Probability / 100,
            ExpectedCloseDate = o.ExpectedCloseDate,
            OwnerId = o.OwnerId,
            OwnerName = o.Owner != null ? o.Owner.FullName : null,
            Note = o.Note,
            CreatedAt = o.CreatedAt,
        }), loadOptions);

    [HttpPost]
    [Authorize(Policy = Permissions.OpportunitiesManage)]
    public async Task<IActionResult> Create(OpportunityInput input)
    {
        var opportunity = new Opportunity { OwnerId = User.GetUserId() };
        Apply(opportunity, input);
        db.Opportunities.Add(opportunity);
        await db.SaveChangesAsync();
        return Ok(new { opportunity.Id });
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.OpportunitiesManage)]
    public async Task<IActionResult> Update(int id, OpportunityInput input)
    {
        var opportunity = await db.Opportunities.FindAsync(id);
        if (opportunity is null) return NotFound();
        var stageChanged = opportunity.Stage != input.Stage;
        Apply(opportunity, input);
        // Moving to another stage resets the probability to that stage's default unless one was sent.
        if (stageChanged && input.Probability is null) opportunity.Probability = DefaultProbability(input.Stage);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.OpportunitiesManage)]
    public async Task<IActionResult> Delete(int id)
    {
        await db.Opportunities.Where(o => o.Id == id).ExecuteDeleteAsync();
        return NoContent();
    }

    private static void Apply(Opportunity o, OpportunityInput input)
    {
        o.Title = input.Title.Trim();
        o.CustomerId = input.CustomerId;
        o.Stage = input.Stage;
        o.Value = input.Value;
        o.Probability = input.Probability ?? DefaultProbability(input.Stage);
        o.ExpectedCloseDate = input.ExpectedCloseDate;
        if (input.OwnerId is not null) o.OwnerId = input.OwnerId;
        o.Note = input.Note;
    }
}

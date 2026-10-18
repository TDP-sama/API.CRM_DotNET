using System.Security.Claims;
using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace API.CRM_DotNET.Infrastructure;

/// <summary>
/// Binds the query string a DevExtreme data source sends (skip, take, sort, filter, group,
/// totalSummary...) so list endpoints can hand it straight to DataSourceLoader.LoadAsync.
/// </summary>
[ModelBinder(BinderType = typeof(DataSourceLoadOptionsBinder))]
public class DataSourceLoadOptions : DataSourceLoadOptionsBase
{
    /// <summary>
    /// For queries with computed columns (correlated subqueries such as product stock totals):
    /// SQL Server cannot SUM() over a subquery, so grouping and summaries are done in memory
    /// after filtering in SQL. Only use it on small tables.
    /// </summary>
    public DataSourceLoadOptions WithInMemoryGrouping()
    {
        RemoteGrouping = false;
        return this;
    }
}

public class DataSourceLoadOptionsBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var loadOptions = new DataSourceLoadOptions();
        DataSourceLoadOptionsParser.Parse(loadOptions, key => bindingContext.ValueProvider.GetValue(key).FirstOrDefault());
        bindingContext.Result = ModelBindingResult.Success(loadOptions);
        return Task.CompletedTask;
    }
}

public static class ClaimsPrincipalExtensions
{
    public static int? GetUserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue("sub"), out var id) ? id : null;
}

/// <summary>Business rule violation; ApiExceptionFilter turns it into a 400 with the message.</summary>
public class BusinessException(string message) : Exception(message);

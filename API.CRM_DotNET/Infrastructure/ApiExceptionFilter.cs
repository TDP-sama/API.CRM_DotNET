using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace API.CRM_DotNET.Infrastructure;

/// <summary>Maps business and database errors to readable 400/409 responses of the form { message }.</summary>
public class ApiExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case BusinessException ex:
                context.Result = new BadRequestObjectResult(new { message = ex.Message });
                context.ExceptionHandled = true;
                break;
            // ExecuteUpdate/ExecuteDelete throw SqlException directly; SaveChanges wraps it.
            case DbUpdateException { InnerException: SqlException } or SqlException:
                var sql = context.Exception as SqlException ?? (SqlException)context.Exception.InnerException!;
                var message = sql.Number switch
                {
                    547 => "Không thể xoá/cập nhật vì dữ liệu đang được sử dụng ở nơi khác.",
                    2601 or 2627 => "Dữ liệu bị trùng (mã hoặc tên đã tồn tại).",
                    _ => null,
                };
                if (message is null) return;
                context.Result = new ConflictObjectResult(new { message });
                context.ExceptionHandled = true;
                break;
        }
    }
}

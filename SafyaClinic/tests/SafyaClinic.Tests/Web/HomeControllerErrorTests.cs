using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Web.Controllers;
using SafyaClinic.Web.Models;
using Xunit;

namespace SafyaClinic.Tests.Web;

public class HomeControllerErrorTests
{
    [Fact]
    public void Error_page_shows_only_a_reference_code()
    {
        var controller = new HomeController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "REF-123" }
            }
        };

        var view = Assert.IsType<ViewResult>(controller.Error());

        var model = Assert.IsType<ErrorViewModel>(view.Model);
        Assert.Equal("REF-123", model.RequestId);
        Assert.True(model.ShowRequestId);
    }
}
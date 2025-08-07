using Microsoft.AspNetCore.Mvc;

namespace Drx.Sdk.Style.Components;

public class ModStoreMarketViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(string? percentText = null, string? title = null, string? subtitle = null)
    {
        ViewData["PercentText"] = percentText ?? "+245%";
        ViewData["Title"] = title ?? "Growth";
        ViewData["Subtitle"] = subtitle ?? "Invest and watch your money grow";
        return View("Default");
    }
}
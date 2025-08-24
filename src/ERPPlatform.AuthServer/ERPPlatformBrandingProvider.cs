﻿using Microsoft.Extensions.Localization;
using ERPPlatform.Localization;
using Volo.Abp.Ui.Branding;
using Volo.Abp.DependencyInjection;

namespace ERPPlatform;

[Dependency(ReplaceServices = true)]
public class ERPPlatformBrandingProvider : DefaultBrandingProvider
{
    private IStringLocalizer<ERPPlatformResource> _localizer;

    public ERPPlatformBrandingProvider(IStringLocalizer<ERPPlatformResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];
}

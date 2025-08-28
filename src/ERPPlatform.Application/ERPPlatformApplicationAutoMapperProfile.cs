using System.Linq;
using AutoMapper;
using Volo.Abp.AuditLogging;
using ERPPlatform.LogAnalytics;

namespace ERPPlatform;

public class ERPPlatformApplicationAutoMapperProfile : Profile
{
    public ERPPlatformApplicationAutoMapperProfile()
    {
        ConfigureLogAnalyticsMappings();
    }

    private void ConfigureLogAnalyticsMappings()
    {
        // Audit Log mappings
        CreateMap<AuditLog, RecentAuditLogDto>()
            .ForMember(dest => dest.ExecutionTime, opt => opt.MapFrom(src => src.ExecutionTime))
            .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.UserId.ToString()))
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.UserName))
            .ForMember(dest => dest.ServiceName, opt => opt.MapFrom(src => src.Actions != null && src.Actions.Count > 0 ? src.Actions.FirstOrDefault().ServiceName ?? "Unknown" : "Unknown"))
            .ForMember(dest => dest.MethodName, opt => opt.MapFrom(src => src.Actions != null && src.Actions.Count > 0 ? src.Actions.FirstOrDefault().MethodName ?? "Unknown" : "Unknown"))
            .ForMember(dest => dest.ExecutionDuration, opt => opt.MapFrom(src => src.ExecutionDuration))
            .ForMember(dest => dest.ClientIpAddress, opt => opt.MapFrom(src => src.ClientIpAddress))
            .ForMember(dest => dest.BrowserInfo, opt => opt.MapFrom(src => src.BrowserInfo))
            .ForMember(dest => dest.HttpMethod, opt => opt.MapFrom(src => src.HttpMethod))
            .ForMember(dest => dest.Url, opt => opt.MapFrom(src => src.Url))
            .ForMember(dest => dest.HttpStatusCode, opt => opt.MapFrom(src => src.HttpStatusCode))
            .ForMember(dest => dest.HasException, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.Exceptions)))
            .ForMember(dest => dest.Exception, opt => opt.MapFrom(src => src.Exceptions));

        // Serilog Entry mappings - Basic mapping for existing properties
        CreateMap<SerilogEntry, SerilogEntryDto>();
        
        // Additional mappings can be added here as needed
        // CreateMap<SourceClass, DestinationDto>();
    }
}

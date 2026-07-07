using Microsoft.Extensions.DependencyInjection;

using ServiceDeskLite.Application.Agents.GetAgents;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Common.Validation;
using ServiceDeskLite.Application.Tickets.AddComment;
using ServiceDeskLite.Application.Tickets.AssignTicket;
using ServiceDeskLite.Application.Tickets.ChangeTicketStatus;
using ServiceDeskLite.Application.Tickets.CreateTicket;
using ServiceDeskLite.Application.Tickets.GetAuditEvents;
using ServiceDeskLite.Application.Tickets.GetDashboardSummary;
using ServiceDeskLite.Application.Tickets.GetTicketById;
using ServiceDeskLite.Application.Tickets.SearchTickets;
using ServiceDeskLite.Application.Tickets.UpdateTicket;

namespace ServiceDeskLite.Application.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();

        // Validators
        services.AddScoped<ICommandValidator<CreateTicketCommand>, CreateTicketValidator>();
        services.AddScoped<ICommandValidator<AddCommentCommand>, AddCommentValidator>();
        services.AddScoped<ICommandValidator<AssignTicketCommand>, AssignTicketValidator>();
        services.AddScoped<ICommandValidator<UpdateTicketCommand>, UpdateTicketValidator>();

        // Handlers
        services.AddScoped<CreateTicketHandler>();
        services.AddScoped<GetTicketByIdHandler>();
        services.AddScoped<SearchTicketsHandler>();
        services.AddScoped<ChangeTicketStatusHandler>();
        services.AddScoped<AssignTicketHandler>();
        services.AddScoped<AddCommentHandler>();
        services.AddScoped<UpdateTicketHandler>();
        services.AddScoped<GetAuditEventsHandler>();
        services.AddScoped<GetDashboardSummaryHandler>();
        services.AddScoped<GetAgentsHandler>();

        return services;
    }
}

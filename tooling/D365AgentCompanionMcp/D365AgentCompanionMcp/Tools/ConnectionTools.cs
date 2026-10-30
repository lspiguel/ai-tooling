// <copyright file="ConnectionTools.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    using System;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Approvals;
    using Lspiguel.Xrm.D365AgentCompanionMcp.Common;
    using Microsoft.Crm.Sdk.Messages;
    using Microsoft.Xrm.Sdk.Query;
    using Newtonsoft.Json.Linq;

    /// <summary>The connection Read tools, <c>get_connection_info</c> and <c>who_am_i</c> (SPEC §6.5).</summary>
    internal static class ConnectionTools
    {
        /// <summary>The <c>get_connection_info</c> tool name.</summary>
        public const string GetConnectionInfo = "get_connection_info";

        /// <summary>The <c>who_am_i</c> tool name.</summary>
        public const string WhoAmI = "who_am_i";

        private const string SystemUser = "systemuser";

        /// <summary>Defines <c>get_connection_info</c>.</summary>
        /// <returns>The definition.</returns>
        public static ToolDefinition GetConnectionInfoTool() => ToolDefinition.Simple(
            GetConnectionInfo,
            ToolTier.Read,
            "One connection: organization, platform version, base language, the user it runs as, whether it is confirmed as a development environment, the approval scopes in force, and settings. Compare organization.url with `pac org who` before running PAC CLI.",
            InputSchema.ForConnection().WithReason(required: false),
            ConnectionInfoOutput(),
            RunGetConnectionInfo);

        /// <summary>Defines <c>who_am_i</c>.</summary>
        /// <returns>The definition.</returns>
        public static ToolDefinition WhoAmITool() => ToolDefinition.Simple(
            WhoAmI,
            ToolTier.Read,
            "The connected user's identity read from Dataverse: IDs, first, last and full name, sign-in name. Use the names for WIP solution naming.",
            InputSchema.ForConnection().WithReason(required: false),
            OutputSchema.Object(
                ("userId", OutputSchema.Guid()),
                ("businessUnitId", OutputSchema.Guid()),
                ("organizationId", OutputSchema.Guid()),
                ("firstName", OutputSchema.NullableString()),
                ("lastName", OutputSchema.NullableString()),
                ("fullName", OutputSchema.NullableString()),
                ("domainName", OutputSchema.NullableString()),
                ("isApplicationUser", OutputSchema.Boolean()),
                ("impersonating", OutputSchema.Boolean())),
            RunWhoAmI);

        private static JObject ConnectionInfoOutput()
        {
            var organization = OutputSchema.Object(
                ("id", OutputSchema.Guid()),
                ("uniqueName", OutputSchema.String()),
                ("friendlyName", OutputSchema.String()),
                ("url", OutputSchema.String()),
                ("version", OutputSchema.String()),
                ("baseLanguageCode", OutputSchema.Integer()));
            var user = OutputSchema.Object(("id", OutputSchema.Guid()), ("name", OutputSchema.NullableString()));
            var settings = OutputSchema.Object(
                ("recordQueriesEnabled", OutputSchema.Boolean()),
                ("publisherCreationEnabled", OutputSchema.Boolean()),
                ("maxPageSize", OutputSchema.Integer()),
                ("responseCapBytes", OutputSchema.Integer()),
                ("approvalTimeoutSeconds", OutputSchema.Integer()));

            return OutputSchema.Object(
                ("connectionId", OutputSchema.String()),
                ("connectionName", OutputSchema.String()),
                ("organization", organization),
                ("user", user),
                ("devEnvironmentConfirmed", OutputSchema.Boolean()),
                ("devEnvironmentScope", OutputSchema.NullableEnum("once", "session", "environment", "always")),
                ("readApprovalScope", OutputSchema.NullableEnum("session", "environment", "always")),
                ("settings", settings));
        }

        private static JObject RunGetConnectionInfo(ToolExecution execution)
        {
            var binding = execution.Binding;
            var service = execution.Service;
            var version = ((RetrieveVersionResponse)service.Execute(new RetrieveVersionRequest())).Version;
            var organization = service.Retrieve("organization", binding.OrganizationId, new ColumnSet("languagecode"));
            var user = service.Retrieve(SystemUser, binding.UserId, new ColumnSet("fullname"));
            var host = execution.Host;
            var devScope = host.DevEnvironmentScope(binding);
            var limits = host.Settings.Read(s => new { s.MaxPageSize, s.ResponseCapCharacters, s.ApprovalTimeoutSeconds });

            return new JObject
            {
                ["connectionId"] = binding.ConnectionId,
                ["connectionName"] = binding.ConnectionName,
                ["organization"] = new JObject
                {
                    ["id"] = ValueFormats.FormatGuid(binding.OrganizationId),
                    ["uniqueName"] = binding.OrganizationUniqueName,
                    ["friendlyName"] = binding.OrganizationFriendlyName,
                    ["url"] = binding.OrganizationUrl,
                    ["version"] = version,
                    ["baseLanguageCode"] = organization.GetAttributeValue<int>("languagecode"),
                },
                ["user"] = new JObject
                {
                    ["id"] = ValueFormats.FormatGuid(binding.UserId),
                    ["name"] = EmptyToNull(user.GetAttributeValue<string>("fullname")),
                },
                ["devEnvironmentConfirmed"] = devScope != null,
                ["devEnvironmentScope"] = ValueFormats.NullableString(devScope.ToLabel()),
                ["readApprovalScope"] = ValueFormats.NullableString(host.Approvals.Find(ApprovalKind.ReadApproval, binding.OrganizationId)?.Scope.ToLabel()),
                ["settings"] = new JObject
                {
                    ["recordQueriesEnabled"] = binding.RecordQueriesEnabled,
                    ["publisherCreationEnabled"] = binding.PublisherCreationEnabled,
                    ["maxPageSize"] = limits.MaxPageSize,
                    ["responseCapBytes"] = limits.ResponseCapCharacters,
                    ["approvalTimeoutSeconds"] = limits.ApprovalTimeoutSeconds,
                },
            };
        }

        private static JObject RunWhoAmI(ToolExecution execution)
        {
            var service = execution.Service;
            var who = (WhoAmIResponse)service.Execute(new WhoAmIRequest());
            var user = service.Retrieve(SystemUser, who.UserId, new ColumnSet("firstname", "lastname", "fullname", "domainname", "applicationid"));

            return new JObject
            {
                ["userId"] = ValueFormats.FormatGuid(who.UserId),
                ["businessUnitId"] = ValueFormats.FormatGuid(who.BusinessUnitId),
                ["organizationId"] = ValueFormats.FormatGuid(who.OrganizationId),
                ["firstName"] = EmptyToNull(user.GetAttributeValue<string>("firstname")),
                ["lastName"] = EmptyToNull(user.GetAttributeValue<string>("lastname")),
                ["fullName"] = EmptyToNull(user.GetAttributeValue<string>("fullname")),
                ["domainName"] = EmptyToNull(user.GetAttributeValue<string>("domainname")),
                ["isApplicationUser"] = user.GetAttributeValue<Guid?>("applicationid") is Guid applicationId && applicationId != Guid.Empty,
                ["impersonating"] = execution.Binding.Impersonating,
            };
        }

        private static JToken EmptyToNull(string? value) => ValueFormats.NullableString(string.IsNullOrEmpty(value) ? null : value);
    }
}

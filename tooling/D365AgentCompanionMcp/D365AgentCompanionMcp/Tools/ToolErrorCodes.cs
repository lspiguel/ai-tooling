// <copyright file="ToolErrorCodes.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    /// <summary>The <c>ToolError</c> codes of SPEC §6.2.</summary>
    internal static class ToolErrorCodes
    {
        /// <summary>Missing, wrongly typed or out-of-range argument, or a rule broken.</summary>
        public const string InvalidArgument = "invalid_argument";

        /// <summary>No plugin tab is bound to <c>connection</c>.</summary>
        public const string ConnectionNotFound = "connection_not_found";

        /// <summary>The tab closed or changed connection while the call waited.</summary>
        public const string ConnectionClosed = "connection_closed";

        /// <summary>A Write or Publisher tool on a connection not confirmed as development.</summary>
        public const string DevEnvironmentNotConfirmed = "dev_environment_not_confirmed";

        /// <summary>The person denied the call.</summary>
        public const string DeniedByUser = "denied_by_user";

        /// <summary>The approval timed out.</summary>
        public const string DeniedNoResponse = "denied_no_response";

        /// <summary>The tool is switched off for this connection: <c>create_publisher</c> (from Phase 6, the only tool).</summary>
        public const string ToolDisabled = "tool_disabled";

        /// <summary>A named item does not exist.</summary>
        public const string NotFound = "not_found";

        /// <summary>A unique name is already taken.</summary>
        public const string AlreadyExists = "already_exists";

        /// <summary>An add or remove against a managed solution.</summary>
        public const string ManagedSolution = "managed_solution";

        /// <summary>The cursor is unknown, expired, or belongs to another connection or tool.</summary>
        public const string InvalidCursor = "invalid_cursor";

        /// <summary>The host is stopping; the call did not run.</summary>
        public const string ServerStopping = "server_stopping";

        /// <summary>The client closed or cancelled the request before the call ran. Logged only (D9).</summary>
        public const string CancelledByClient = "cancelled_by_client";

        /// <summary>The platform returned a fault.</summary>
        public const string DataverseError = "dataverse_error";

        /// <summary>A component's current version differs from the <c>expectedVersion</c> passed (SPEC 6.8).</summary>
        public const string VersionConflict = "version_conflict";

        /// <summary>The component is managed — a first-party web resource, step or custom API (SPEC 6.8).</summary>
        public const string ManagedComponent = "managed_component";

        /// <summary>An existing component is not in the named WIP (SPEC 6.8).</summary>
        public const string NotInSolution = "not_in_solution";
    }
}

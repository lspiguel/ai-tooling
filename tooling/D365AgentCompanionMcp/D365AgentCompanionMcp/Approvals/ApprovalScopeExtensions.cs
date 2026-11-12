// <copyright file="ApprovalScopeExtensions.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Approvals
{
    using System;

    /// <summary>Labels for scopes and kinds, as tool results, the call log and the windows show them.</summary>
    internal static class ApprovalScopeExtensions
    {
        /// <summary>Gets the lowercase label: <c>once</c>, <c>session</c>, <c>environment</c>, <c>tenant</c> or <c>global</c> (the enum's <c>Always</c>).</summary>
        /// <param name="scope">The scope.</param>
        /// <returns>The label.</returns>
        public static string ToLabel(this ApprovalScope scope) => scope == ApprovalScope.Always ? "global" : scope.ToString().ToLowerInvariant();

        /// <summary>Gets the label, or <c>null</c> when there is no scope.</summary>
        /// <param name="scope">The scope, or <c>null</c>.</param>
        /// <returns>The label, or <c>null</c>.</returns>
        public static string? ToLabel(this ApprovalScope? scope) => scope?.ToLabel();

        /// <summary>Gets the name the windows show: <i>Once</i>, <i>Session only</i>, <i>Per environment</i>, <i>Per tenant</i> or <i>Global</i>.</summary>
        /// <param name="scope">The scope.</param>
        /// <returns>The display name.</returns>
        public static string ToDisplay(this ApprovalScope scope)
        {
            switch (scope)
            {
                case ApprovalScope.Once:
                    return "Once";
                case ApprovalScope.Session:
                    return "Session only";
                case ApprovalScope.Environment:
                    return "Per environment";
                case ApprovalScope.Tenant:
                    return "Per tenant";
                default:
                    return "Global";
            }
        }

        /// <summary>Gets the name the Active Approvals window shows for a kind: <i>Read</i>, <i>Records</i>, <i>Write</i> or <i>Dev env</i>.</summary>
        /// <param name="kind">The kind.</param>
        /// <returns>The display name.</returns>
        public static string ToDisplay(this ApprovalKind kind)
        {
            switch (kind)
            {
                case ApprovalKind.ReadApproval:
                    return "Read";
                case ApprovalKind.RecordRead:
                    return "Records";
                case ApprovalKind.Write:
                    return "Write";
                case ApprovalKind.DevEnvironment:
                    return "Dev env";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        /// <summary>Tells whether a grant of a kind may have a scope: Write and the dev-environment confirmation stop at one environment (SPEC §7).</summary>
        /// <param name="kind">The kind.</param>
        /// <param name="scope">The scope.</param>
        /// <returns><c>true</c> when the combination is allowed as a stored grant.</returns>
        public static bool Allows(this ApprovalKind kind, ApprovalScope scope)
        {
            if (scope == ApprovalScope.Once)
            {
                return false;
            }

            return (kind != ApprovalKind.Write && kind != ApprovalKind.DevEnvironment) || scope == ApprovalScope.Session || scope == ApprovalScope.Environment;
        }
    }
}

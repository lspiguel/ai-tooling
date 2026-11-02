// <copyright file="ApprovalScopeExtensions.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Approvals
{
    /// <summary>Labels for scopes, as tool results and the call log show them.</summary>
    internal static class ApprovalScopeExtensions
    {
        /// <summary>Gets the lowercase label: <c>once</c>, <c>session</c>, <c>environment</c> or <c>always</c>.</summary>
        /// <param name="scope">The scope.</param>
        /// <returns>The label.</returns>
        public static string ToLabel(this ApprovalScope scope) => scope.ToString().ToLowerInvariant();

        /// <summary>Gets the label, or <c>null</c> when there is no scope.</summary>
        /// <param name="scope">The scope, or <c>null</c>.</param>
        /// <returns>The label, or <c>null</c>.</returns>
        public static string? ToLabel(this ApprovalScope? scope) => scope?.ToLabel();
    }
}

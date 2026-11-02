// <copyright file="IApprovalPrompt.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Approvals
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Shows an approval dialog for a tab's connection; replaced by a fake in tests.</summary>
    internal interface IApprovalPrompt
    {
        /// <summary>Asks the person. Cancelling the token withdraws the dialog and cancels the task.</summary>
        /// <param name="request">What to show.</param>
        /// <param name="cancellation">Signalled on timeout, client cancellation, connection change or host stop.</param>
        /// <returns>The person's decision.</returns>
        Task<ApprovalDecision> PromptAsync(ApprovalRequest request, CancellationToken cancellation);
    }
}

// <copyright file="IFileRootPrompt.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Files
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Asks the person for a File root in the tab's connection; replaced by a fake in tests.</summary>
    internal interface IFileRootPrompt
    {
        /// <summary>Asks the person. Cancelling the token withdraws the dialog and cancels the task.</summary>
        /// <param name="request">What to show.</param>
        /// <param name="cancellation">Signalled on timeout, client cancellation, connection change or host stop.</param>
        /// <returns>The choice, or <c>null</c> when the person cancelled.</returns>
        Task<FileRootChoice?> PromptFileRootAsync(FileRootRequest request, CancellationToken cancellation);
    }
}

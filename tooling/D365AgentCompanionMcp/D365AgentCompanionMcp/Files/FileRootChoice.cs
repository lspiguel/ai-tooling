// <copyright file="FileRootChoice.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Files
{
    using Lspiguel.Xrm.D365AgentCompanionMcp.Approvals;

    /// <summary>The folder and level the person chose.</summary>
    internal sealed class FileRootChoice
    {
        /// <summary>Initializes a new instance of the <see cref="FileRootChoice"/> class.</summary>
        /// <param name="path">The folder.</param>
        /// <param name="level"><see cref="ApprovalScope.Tenant"/> or <see cref="ApprovalScope.Environment"/>.</param>
        public FileRootChoice(string path, ApprovalScope level)
        {
            this.Path = path;
            this.Level = level;
        }

        /// <summary>Gets the folder.</summary>
        public string Path { get; }

        /// <summary>Gets the level.</summary>
        public ApprovalScope Level { get; }
    }
}

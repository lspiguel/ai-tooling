// <copyright file="IGrantPersistence.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Approvals
{
    using System.Collections.Generic;

    /// <summary>Where environment and always grants are kept between sessions.</summary>
    internal interface IGrantPersistence
    {
        /// <summary>Loads the persisted grants.</summary>
        /// <returns>The grants; unreadable entries are skipped.</returns>
        IReadOnlyList<Grant> LoadGrants();

        /// <summary>Replaces the persisted grants.</summary>
        /// <param name="grants">Every persistent grant.</param>
        void SaveGrants(IReadOnlyList<Grant> grants);
    }
}

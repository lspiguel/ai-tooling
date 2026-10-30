// <copyright file="DpapiTokenProtector.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Hosting
{
    using System;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>DPAPI for the current Windows user (D8).</summary>
    internal sealed class DpapiTokenProtector : ITokenProtector
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Lspiguel.Xrm.D365AgentCompanionMcp.Token");

        /// <inheritdoc/>
        public string Protect(string token) =>
            Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(token), Entropy, DataProtectionScope.CurrentUser));

        /// <inheritdoc/>
        public string Unprotect(string protectedToken) =>
            Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(protectedToken), Entropy, DataProtectionScope.CurrentUser));
    }
}

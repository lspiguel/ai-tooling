// <copyright file="ITokenProtector.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Hosting
{
    /// <summary>Encrypts the bearer token at rest; replaced by a fake in tests.</summary>
    internal interface ITokenProtector
    {
        /// <summary>Encrypts a token.</summary>
        /// <param name="token">The token.</param>
        /// <returns>The protected value, Base64.</returns>
        string Protect(string token);

        /// <summary>Decrypts a token. Throws when the value cannot be read.</summary>
        /// <param name="protectedToken">The protected value, Base64.</param>
        /// <returns>The token.</returns>
        string Unprotect(string protectedToken);
    }
}

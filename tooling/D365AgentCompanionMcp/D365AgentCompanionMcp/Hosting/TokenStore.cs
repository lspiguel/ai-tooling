// <copyright file="TokenStore.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Hosting
{
    using System;
    using System.Security.Cryptography;

    /// <summary>
    /// The host's bearer token (SPEC §5, D8): generated on first use, kept across host starts and XrmToolBox
    /// restarts, DPAPI-protected in the plugin settings, and changed only by <see cref="Regenerate"/>.
    /// </summary>
    internal sealed class TokenStore
    {
        /// <summary>Shown when no token was saved.</summary>
        public const string GeneratedNotice = "A new bearer token was generated, because none was saved. Copy it into each client configuration.";

        /// <summary>Shown when the saved token could not be read.</summary>
        public const string UnreadableNotice = "The saved bearer token could not be read — for example, the settings came from another Windows user — so a new one was generated. Update every client configuration.";

        /// <summary>Shown after Regenerate.</summary>
        public const string RegeneratedNotice = "The bearer token was regenerated. Every client configuration must be updated.";

        private readonly HostSettingsStore settings;
        private readonly ITokenProtector protector;
        private volatile string? token;

        /// <summary>Initializes a new instance of the <see cref="TokenStore"/> class.</summary>
        /// <param name="settings">The settings holding the protected token.</param>
        /// <param name="protector">Encrypts the token at rest.</param>
        public TokenStore(HostSettingsStore settings, ITokenProtector protector)
        {
            this.settings = settings;
            this.protector = protector;
        }

        /// <summary>Raised after the token changes.</summary>
        public event EventHandler? Changed;

        /// <summary>Gets the current token, or <c>null</c> before <see cref="Load"/>.</summary>
        public string? Token => this.token;

        /// <summary>Loads the saved token, or generates and saves a new one when it is missing or unreadable.</summary>
        /// <returns>A notice for every tab when a new token was generated; otherwise <c>null</c>.</returns>
        public string? Load()
        {
            var saved = this.settings.Read(s => s.ProtectedToken);
            if (!string.IsNullOrEmpty(saved))
            {
                try
                {
                    this.token = this.protector.Unprotect(saved!);
                    if (!string.IsNullOrEmpty(this.token))
                    {
                        return null;
                    }
                }
                catch (Exception ex) when (ex is CryptographicException || ex is FormatException)
                {
                    // Falls through to a new token.
                }

                this.Replace();
                return UnreadableNotice;
            }

            this.Replace();
            return GeneratedNotice;
        }

        /// <summary>Replaces the token. Every client configuration must then be updated (H4).</summary>
        public void Regenerate() => this.Replace();

        private static string NewToken()
        {
            var bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
            }

            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private void Replace()
        {
            var fresh = NewToken();
            this.settings.Update(s => s.ProtectedToken = this.protector.Protect(fresh));
            this.token = fresh;
            this.Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}

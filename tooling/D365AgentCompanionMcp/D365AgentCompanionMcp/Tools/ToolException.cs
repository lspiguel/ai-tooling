// <copyright file="ToolException.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.Tools
{
    using System;
    using Newtonsoft.Json.Linq;

    /// <summary>A tool failure that reaches the agent as a <c>ToolError</c>.</summary>
    internal sealed class ToolException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="ToolException"/> class.</summary>
        /// <param name="code">A code from <see cref="ToolErrorCodes"/>.</param>
        /// <param name="message">Plain text, for the person and the agent.</param>
        /// <param name="details">Optional details.</param>
        public ToolException(string code, string message, JObject? details = null)
            : base(message)
        {
            this.Code = code;
            this.Details = details;
        }

        /// <summary>Gets the error code.</summary>
        public string Code { get; }

        /// <summary>Gets the details, or <c>null</c>.</summary>
        public JObject? Details { get; }

        /// <summary>Creates an <c>invalid_argument</c> error naming the argument.</summary>
        /// <param name="argument">The argument.</param>
        /// <param name="message">What is wrong.</param>
        /// <returns>The exception.</returns>
        public static ToolException InvalidArgument(string argument, string message) =>
            new ToolException(ToolErrorCodes.InvalidArgument, message, new JObject { ["argument"] = argument });
    }
}

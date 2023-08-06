using DBot.Models.HttpModels.Interaction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static DBot.Models.EventData.Interaction;

namespace DBot.Models.EventData
{
    internal sealed class Interaction
    {
        public enum InteractionType
        {
            PING = 1,
            APPLICATION_COMMAND,
            MESSAGE_COMPONENT,
            APPLICATION_COMMAND_AUTOCOMPLETE,
            MODAL_SUBMIT
        }

        public interface InteractionData { }

        internal sealed class PingInteractionData : InteractionData 
        {   }

        internal sealed class AppCommandInteractionOption : InteractionData
        {
            public AppCommandInteractionOption(string name, AppCommandOptionType type, string? description, JsonElement? value, bool? required, AppCommandInteractionOption[]? options)
            {
                Name = name;
                Type = (int)type;
                Description = description;
                Value = value;
                Required = required;
                Options = options;
            }

            [JsonConstructor]
            public AppCommandInteractionOption(string? id, string name, int type, string? description, JsonElement? value, bool? required, AppCommandInteractionOption[]? options)
            {
                Id = id;
                Name = name;
                Type = type;
                Description = description;
                Value = value;
                Required = required;
                Options = options;
            }

            public static GlobalCommandComparer Comparer { get; } = new GlobalCommandComparer();

            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("type")]
            public int Type { get; set; }

            [JsonPropertyName("description")]
            public string? Description { get; set; }

            [JsonPropertyName("value")]
            public JsonElement? Value { get; set; }

            [JsonPropertyName("required")]
            public bool? Required { get; set; }

            [JsonPropertyName("options")]
            public AppCommandInteractionOption[]? Options { get; set; }

            public enum AppCommandOptionType
            {
                SUB_COMMAND = 1,
                SUB_COMMAND_GROUP = 2,
                STRING = 3,
                INTEGER = 4,
                BOOLEAN = 5,
                USER = 6,
                CHANNEL = 7,
                ROLE = 8,
                MENTIONABLE = 9,
                NUMBER = 10,
                ATTACHMENT = 11
            }

            public class GlobalCommandComparer : EqualityComparer<GlobalCommand<AppCommandInteractionOption>[]>
            {
                public override bool Equals(GlobalCommand<AppCommandInteractionOption>[]? x, GlobalCommand<AppCommandInteractionOption>[]? y)
                {
                    if (x is null || y is null)
                        return false;

                    if (x.Length != y.Length)
                        return false;

                    for (int q = 0; q < x.Length; q++)
                    {
                        var first = x[q];
                        var second = y[q];

                        if (first.Name != second.Name ||
                            first.Description != second.Description)
                            return false;

                        if (first.Options is null && second.Options is null)
                            continue;

                        if ((first.Options is null && second.Options is not null) || (first.Options is not null && second.Options is null))
                            return false;
                        
                        if (first.Options!.Length != second.Options!.Length)
                            return false;

                        var thisOpts = first.Options!.OrderBy(x => x.Name).ToArray();
                        var inpOpts = second.Options!.OrderBy(x => x.Name).ToArray();

                        for (int i = 0; i < thisOpts.Length; i++)
                        {
                            if (!thisOpts[i].DeepEquals(inpOpts[i]))
                                return false;
                        }
                    }
                    return true;
                }

                public override int GetHashCode(GlobalCommand<AppCommandInteractionOption>[] obj)
                {
                    return HashCode.Combine(obj.Length);
                }
            }

            public bool DeepEquals(object? obj)
            {
                if (obj is null)
                    return false;
                var appObj = obj as AppCommandInteractionOption;
                if (appObj is null)
                    return false;

                if (this.Name != appObj.Name ||
                    this.Description != appObj.Description)
                    return false;

                if ((this.Options is not null && appObj.Options is null) || (appObj.Options is not null && this.Options is null))
                    return false;

                if (this.Options is null && appObj.Options is null)
                    return true;

                if (this.Options!.Length != appObj.Options!.Length)
                    return false;

                var thisOpts = this.Options.OrderBy(x => x.Name).ToArray();
                var inpOpts = appObj.Options.OrderBy(x => x.Name).ToArray();

                for (int i = 0; i < thisOpts.Length; i++)
                {
                    if (!thisOpts[i].DeepEquals(inpOpts[i]))
                        return false;
                }

                return true;
            }
        }

        internal sealed class InteractionCreate<Payload> : EventDataBase
            where Payload : class, InteractionData
        {
            public InteractionCreate(string id, string appId, InteractionType interactionType, Payload? data, string? guildId, string? channelId, string token, MessageEventCreate? message)
            {
                Id = id;
                AppId = appId;
                InteractionType = interactionType;
                Data = data;
                GuildId = guildId;
                ChannelId = channelId;
                Token = token;
                Message = message;
            }

            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("application_id")]
            public string AppId { get; set; }

            [JsonPropertyName("type")]
            public InteractionType InteractionType { get; set; }

            [JsonPropertyName("data")]
            public Payload? Data { get; set; }

            [JsonPropertyName("guild_id")]
            public string? GuildId { get; set; }

            [JsonPropertyName("channel_id")]
            public string? ChannelId { get; set; }

            [JsonPropertyName("token")]
            public string Token { get; set; }

            [JsonPropertyName("message")]
            public MessageEventCreate? Message { get; set; }
        }
    }
}

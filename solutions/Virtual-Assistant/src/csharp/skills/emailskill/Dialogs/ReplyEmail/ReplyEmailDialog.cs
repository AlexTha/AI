﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EmailSkill.Dialogs.Shared.Resources;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Solutions.Extensions;
using Microsoft.Bot.Solutions.Skills;
using Microsoft.Graph;

namespace EmailSkill
{
    public class ReplyEmailDialog : EmailSkillDialog
    {
        public ReplyEmailDialog(
            ISkillConfiguration services,
            IStatePropertyAccessor<EmailSkillState> emailStateAccessor,
            IStatePropertyAccessor<DialogState> dialogStateAccessor,
            IMailSkillServiceManager serviceManager)
            : base(nameof(ReplyEmailDialog), services, emailStateAccessor, dialogStateAccessor, serviceManager)
        {
            var replyEmail = new WaterfallStep[]
            {
                GetAuthToken,
                AfterGetAuthToken,
                CollectSelectedEmail,
                CollectAdditionalText,
                ConfirmBeforeSending,
                ReplyEmail,
            };

            var showEmail = new WaterfallStep[]
            {
                ShowEmails,
            };

            var updateSelectMessage = new WaterfallStep[]
            {
                UpdateMessage,
                PromptUpdateMessage,
                AfterUpdateMessage,
            };
            AddDialog(new WaterfallDialog(Actions.Reply, replyEmail));

            // Define the conversation flow using a waterfall model.
            AddDialog(new WaterfallDialog(Actions.Show, showEmail));
            AddDialog(new WaterfallDialog(Actions.UpdateSelectMessage, updateSelectMessage));

            InitialDialogId = Actions.Reply;
        }

        public async Task<DialogTurnResult> ReplyEmail(WaterfallStepContext sc, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                var confirmResult = (bool)sc.Result;
                if (confirmResult)
                {
                    var state = await EmailStateAccessor.GetAsync(sc.Context);
                    var token = state.MsGraphToken;
                    var message = state.Message.FirstOrDefault();
                    var content = state.Content;

                    var service = ServiceManager.InitMailService(token, state.GetUserTimeZone());

                    // reply user message.
                    if (message != null)
                    {
                        await service.ReplyToMessageAsync(message.Id, content);
                    }

                    var nameListString = $"To: {message?.From.EmailAddress.Name}";
                    if (message?.ToRecipients.Count() > 1)
                    {
                        nameListString += $" + {message.ToRecipients.Count() - 1} more";
                    }

                    var emailCard = new EmailCardData
                    {
                        Subject = "RE: " + message?.Subject,
                        NameList = nameListString,
                        EmailContent = state.Content,
                    };
                    var replyMessage = sc.Context.Activity.CreateAdaptiveCardReply(EmailSharedResponses.SentSuccessfully, "Dialogs/Shared/Resources/Cards/EmailWithOutButtonCard.json", emailCard);

                    await sc.Context.SendActivityAsync(replyMessage);
                }
                else
                {
                    await sc.Context.SendActivityAsync(sc.Context.Activity.CreateReply(EmailSharedResponses.CancellingMessage));
                }
            }
            catch (Exception ex)
            {
                throw await HandleDialogExceptions(sc, ex);
            }

            await ClearConversationState(sc);
            return await sc.EndDialogAsync(true);
        }
    }
}

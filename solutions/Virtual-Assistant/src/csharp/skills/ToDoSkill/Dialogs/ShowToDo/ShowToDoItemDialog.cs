﻿using Luis;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Solutions.Extensions;
using Microsoft.Bot.Solutions.Skills;
using System;
using System.Threading;
using System.Threading.Tasks;
using ToDoSkill.Dialogs.Shared.Resources;
using ToDoSkill.Dialogs.ShowToDo.Resources;

namespace ToDoSkill
{
    public class ShowToDoItemDialog : ToDoSkillDialog
    {
        public ShowToDoItemDialog(
            SkillConfiguration services,
            IStatePropertyAccessor<ToDoSkillState> accessor,
            IToDoService serviceManager)
            : base(nameof(ShowToDoItemDialog), services, accessor, serviceManager)
        {
            var showToDoTasks = new WaterfallStep[]
           {
                GetAuthToken,
                AfterGetAuthToken,
                ClearContext,
                ShowToDoTasks,
                AddFirstTask,
           };

            var addFirstTask = new WaterfallStep[]
            {
                AskAddFirstTaskConfirmation,
                AfterAskAddFirstTaskConfirmation,
            };

            // Define the conversation flow using a waterfall model.
            AddDialog(new WaterfallDialog(Action.ShowToDoTasks, showToDoTasks));
            AddDialog(new WaterfallDialog(Action.AddFirstTask, addFirstTask));
            AddDialog(new AddToDoItemDialog(_services, _accessor, _serviceManager));

            // Set starting dialog for component
            InitialDialogId = Action.ShowToDoTasks;
        }

        public async Task<DialogTurnResult> ShowToDoTasks(WaterfallStepContext sc, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                var state = await _accessor.GetAsync(sc.Context);
                if (string.IsNullOrEmpty(state.OneNotePageId))
                {
                    await sc.Context.SendActivityAsync(sc.Context.Activity.CreateReply(ShowToDoResponses.SettingUpOneNoteMessage));
                }

                var topIntent = state.LuisResult?.TopIntent().intent;
                if (topIntent == ToDo.Intent.ShowToDo || topIntent == ToDo.Intent.None)
                {
                    var service = await _serviceManager.Init(state.MsGraphToken, state.OneNotePageId);
                    var todosAndPageIdTuple = await service.GetMyToDoList();
                    state.OneNotePageId = todosAndPageIdTuple.Item2;
                    state.AllTasks = todosAndPageIdTuple.Item1;
                }

                var allTasksCount = state.AllTasks.Count;
                var currentTaskIndex = state.ShowToDoPageIndex * state.PageSize;
                state.Tasks = state.AllTasks.GetRange(currentTaskIndex, Math.Min(state.PageSize, allTasksCount - currentTaskIndex));
                if (state.Tasks.Count <= 0)
                {
                    return await sc.NextAsync();
                }
                else
                {
                    Attachment toDoListAttachment = null;
                    if (topIntent == ToDo.Intent.ShowToDo || topIntent == ToDo.Intent.None)
                    {
                        toDoListAttachment = ToAdaptiveCardAttachmentForShowToDos(
                            state.Tasks,
                            state.AllTasks.Count,
                            ShowToDoResponses.ShowToDoTasks,
                            ShowToDoResponses.ReadToDoTasks);
                    }
                    else if (topIntent == ToDo.Intent.Next)
                    {
                        toDoListAttachment = ToAdaptiveCardAttachmentForShowToDos(
                            state.Tasks,
                            state.AllTasks.Count,
                            ShowToDoResponses.ShowNextToDoTasks,
                            null);
                    }
                    else if (topIntent == ToDo.Intent.Previous)
                    {
                        toDoListAttachment = ToAdaptiveCardAttachmentForShowToDos(
                            state.Tasks,
                            state.AllTasks.Count,
                            ShowToDoResponses.ShowPreviousToDoTasks,
                            null);
                    }

                    var toDoListReply = sc.Context.Activity.CreateReply();
                    toDoListReply.Attachments.Add(toDoListAttachment);
                    await sc.Context.SendActivityAsync(toDoListReply);
                    if ((topIntent == ToDo.Intent.ShowToDo || topIntent == ToDo.Intent.None)
                        && allTasksCount > (state.ShowToDoPageIndex + 1) * state.PageSize)
                    {
                        await sc.Context.SendActivityAsync(sc.Context.Activity.CreateReply(ShowToDoResponses.ShowingMoreTasks));
                    }

                    return await sc.EndDialogAsync(true);
                }
            }
            catch (Exception ex)
            {
                await HandleDialogExceptions(sc);
                throw;
            }
        }

        public async Task<DialogTurnResult> AddFirstTask(WaterfallStepContext sc, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await sc.BeginDialogAsync(Action.AddFirstTask);
        }

        public async Task<DialogTurnResult> AskAddFirstTaskConfirmation(WaterfallStepContext sc, CancellationToken cancellationToken = default(CancellationToken))
        {
            var prompt = sc.Context.Activity.CreateReply(ShowToDoResponses.NoToDoTasksPrompt);
            return await sc.PromptAsync(Action.Prompt, new PromptOptions() { Prompt = prompt });
        }

        public async Task<DialogTurnResult> AfterAskAddFirstTaskConfirmation(WaterfallStepContext sc, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                var state = await _accessor.GetAsync(sc.Context);
                var topIntent = state.LuisResult?.TopIntent().intent;
                if (topIntent == ToDo.Intent.ConfirmYes)
                {
                    return await sc.BeginDialogAsync(nameof(AddToDoItemDialog));
                }
                else if (topIntent == ToDo.Intent.ConfirmNo)
                {
                    await sc.Context.SendActivityAsync(sc.Context.Activity.CreateReply(ToDoSharedResponses.ActionEnded));
                    return await sc.EndDialogAsync(true);
                }
                else
                {
                    return await sc.BeginDialogAsync(Action.AddFirstTask);
                }
            }
            catch (Exception ex)
            {
                await HandleDialogExceptions(sc);
                throw;
            }
        }
    }
}
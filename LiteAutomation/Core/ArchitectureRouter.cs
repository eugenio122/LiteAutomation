using System;
using System.Collections.Generic;
using System.Linq;
using LiteAutomation.Enums;
using LiteTools.Core.Languages;

namespace LiteAutomation.Core
{
    public class ArchitectureRouter
    {
        public List<PomPageModel> BuildPomStructure(List<AutomationIntent> linearIntents)
        {
            var pages = new List<PomPageModel>();
            PomPageModel currentPage = null;
            int pageCounter = 1;

            foreach (var intent in linearIntents)
            {
                if (intent.Type == IntentType.NavigateToUrl || intent.Type == IntentType.WaitUrlChange)
                {
                    string safeName = ExtractPageNameFromUrl(intent.Value);
                    if (string.IsNullOrEmpty(safeName)) safeName = $"Page{pageCounter}";

                    currentPage = new PomPageModel
                    {
                        ClassName = $"{safeName}Page",
                        UrlIdentifier = intent.Value
                    };
                    pages.Add(currentPage);
                    pageCounter++;
                }

                if (currentPage == null && intent.Type != IntentType.Unknown)
                {
                    currentPage = new PomPageModel { ClassName = "MainPage", UrlIdentifier = "app" };
                    pages.Add(currentPage);
                }

                if (currentPage != null && intent.Type != IntentType.Unknown && !intent.IsNewStepHeader)
                {
                    currentPage.PageActions.Add(intent);

                    if (!string.IsNullOrEmpty(intent.TargetLocator) && !currentPage.MappedLocators.ContainsKey(intent.TargetLocator))
                    {
                        string locName = $"element_{intent.StepId.Replace(".", "_")}";
                        currentPage.MappedLocators.Add(intent.TargetLocator, locName);
                    }
                }
            }

            return pages;
        }

        public BddScenarioModel BuildBddStructure(List<AutomationIntent> linearIntents)
        {
            var scenario = new BddScenarioModel();
            BddStepModel currentStep = null;

            scenario.Steps.Add(new BddStepModel
            {
                Keyword = "Given",
                TextDescription = LanguageManager.GetString("BddDefaultGiven")
            });

            foreach (var intent in linearIntents)
            {
                if (intent.Type == IntentType.NavigateToUrl)
                {
                    scenario.Steps[0].InternalIntents.Add(intent);
                    continue;
                }

                if (intent.IsNewStepHeader)
                {
                    string keyword = intent.StepDescription.ToLower().Contains("valida") || intent.StepDescription.ToLower().Contains("verifica")
                                     ? "Then" : "When";

                    currentStep = new BddStepModel
                    {
                        Keyword = keyword,
                        TextDescription = $"{LanguageManager.GetString("BddDefaultAction")} {intent.StepDescription.ToLower()}"
                    };
                    scenario.Steps.Add(currentStep);
                }
                else if (currentStep != null)
                {
                    currentStep.InternalIntents.Add(intent);
                }
            }

            scenario.Steps.RemoveAll(s => s.InternalIntents.Count == 0);

            return scenario;
        }

        private string ExtractPageNameFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            try
            {
                var uri = new Uri(url.StartsWith("http") ? url : "http://dummy.com" + url);
                string lastSegment = uri.Segments.LastOrDefault()?.Replace("/", "").Split('?')[0];

                if (string.IsNullOrEmpty(lastSegment) || lastSegment == "index.html")
                    return LanguageManager.GetString("PageNameHome");

                return char.ToUpper(lastSegment[0]) + lastSegment.Substring(1).Replace("-", "").Replace("_", "");
            }
            catch
            {
                return LanguageManager.GetString("PageNameUnknown");
            }
        }
    }
}
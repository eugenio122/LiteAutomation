using System;
using System.Collections.Generic;
using System.Text;
using LiteTools.Core.Languages;

namespace LiteAutomation.Core
{
    public class BddScenarioModel
    {
        public string FeatureTitle { get; set; } = LanguageManager.GetString("BddDefaultScenarioTitle");
        public List<BddStepModel> Steps { get; set; } = new List<BddStepModel>();
    }
}
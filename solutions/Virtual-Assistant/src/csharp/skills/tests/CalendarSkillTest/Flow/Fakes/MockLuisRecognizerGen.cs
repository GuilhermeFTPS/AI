﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Luis;
using Microsoft.Bot.Builder;
using static Luis.Calendar;

namespace CalendarSkillTest.Flow.Fakes
{
    public class MockLuisRecognizer : IRecognizer
    {
        public MockLuisRecognizer()
        {

        }

        public Task<RecognizerResult> RecognizeAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task<T> RecognizeAsync<T>(ITurnContext turnContext, CancellationToken cancellationToken)
            where T : IRecognizerConvert, new()
        {
            T mockResult = new T();

            Type t = typeof(T);
            var text = turnContext.Activity.Text;
            if (t.Name.Equals(typeof(Calendar).Name))
            {
                MockCalendarIntent mockEmail = new MockCalendarIntent(text);

                var test = mockEmail as object;
                mockResult = (T)test;
            }
            else if (t.Name.Equals(typeof(General).Name))
            {
                MockGeneralIntent mockGeneralIntent = new MockGeneralIntent(text);

                var test = mockGeneralIntent as object;
                mockResult = (T)test;
            }

            return mockResult;
        }
    }
}
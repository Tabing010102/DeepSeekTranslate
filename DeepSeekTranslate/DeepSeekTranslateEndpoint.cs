using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;
using XUnity.AutoTranslator.Plugin.Core.Endpoints.Www;

namespace DeepSeekTranslate
{
    public class DeepSeekTranslateEndpoint : WwwEndpoint
    {
        public override string Id => throw new NotImplementedException();

        public override string FriendlyName => throw new NotImplementedException();

        public override void Initialize(IInitializationContext context)
        {
            throw new NotImplementedException();
        }

        public override void OnCreateRequest(IWwwRequestCreationContext context)
        {
            throw new NotImplementedException();
        }

        public override void OnExtractTranslation(IWwwTranslationExtractionContext context)
        {
            throw new NotImplementedException();
        }
    }
}

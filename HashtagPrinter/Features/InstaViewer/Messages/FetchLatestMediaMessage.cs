using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skarynagram.Features.InstaViewer.Messages
{
    internal class FetchLatestMediaMessage : RequestMessage<string>
    {
        public FetchLatestMediaMessage(string value) : base() { }
    }

    
}

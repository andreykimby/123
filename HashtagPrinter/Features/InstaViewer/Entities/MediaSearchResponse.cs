using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skarynagram.Features.InstaViewer.Entities
{
    public record MediaSearchResponse(List<MediaElement> Data, PagingInfo Paging);
}

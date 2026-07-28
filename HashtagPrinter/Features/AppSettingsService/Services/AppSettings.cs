using CommunityToolkit.Mvvm.ComponentModel;
using Skarynagram.Features.AppSettingsService.Repositories;
using Skarynagram.Features.AuthService.Models;
using Skarynagram.Features.PrintService.Service;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skarynagram.Features.AppSettingsService.Services
{


    public class AppSettings
    {
        private readonly IAppSettingsRepository _repository;

        public AppSettings(IAppSettingsRepository repository)
        {
            _repository = repository;
        }

        public string GetSetting(string key) => _repository.Get<string>(key);
        public void SaveSetting(string key, string value) => _repository.Set<string>(key, value);

        public string AppId
        {
            get => _repository.Get<string>("AppId");
            set => _repository.Set<string>("AppId", value);
        }

        public string SelectedPrinter
        {
            get => _repository.Get<string>("SelectedPrinter");
            set => _repository.Set<string>("SelectedPrinter", value);
        }

        public string AppSecret
        {
            get => _repository.Get<string>("AppSecret");
            set => _repository.Set<string>("AppSecret", value);
        }

        public string SelectedHashtag
        {
            get => _repository.Get<string>("SelectedHashtag");
            set => _repository.Set<string>("SelectedHashtag", value);
        }

        public string RedirectUrl
        {
            get => _repository.Get<string>("RedirectUrl");
            set => _repository.Set<string>("RedirectUrl", value);
        }

        public string AuthToken
        {
            get => _repository.Get<string>("AuthToken");
            set => _repository.Set<string>("AuthToken", value);
        }

        public Account SelectedAccount
        {
            get => _repository.Get<Account>("SelectedAccount");
            set => _repository.Set<Account>("SelectedAccount", value);
        }

        public string SelectedPageId
        {
            get => _repository.Get<string>("SelectedPageId");
            set => _repository.Set<string>("SelectedPageId", value);
        }

        public string SelectedHashtagId
        {
            get => _repository.Get<string>("SelectedHashtagId");
            set => _repository.Set<string>("SelectedHashtagId", value);
        }

        /// <summary>
        /// Задержка в миллисекундах между запросами в InstaViewer для получения последних медиа по хэштегу. По умолчанию 360000
        /// </summary>
        public int InstaViewerRequestDelay
        {
            get
            {
                var delay = _repository.Get<int>("ViewerRequestDelay");
                if (delay == 0)
                {
                    return 360000;
                }
                return delay;
            }
            set => _repository.Set<int>("ViewerRequestDelay", value);
        }

        public string TemplatePath
        {
            get => _repository.Get<string>(nameof(TemplatePath));
            set => _repository.Set<string>(nameof(TemplatePath), value);
        }

        public string HeaderValue
        {
            get => _repository.Get<string>(nameof(HeaderValue));
            set => _repository.Set<string>(nameof(HeaderValue), value);
        }

        public string DescriptionValue
        {
            get => _repository.Get<string>(nameof(DescriptionValue));
            set => _repository.Set<string>(nameof(DescriptionValue), value);
        }

        public string ClientPassword
        {
            get => _repository.Get<string>(nameof(ClientPassword));
            set => _repository.Set<string>(nameof(ClientPassword), value);
        }

        public string OpenAiApiKey
        {
            get => _repository.Get<string>(nameof(OpenAiApiKey));
            set => _repository.Set<string>(nameof(OpenAiApiKey), value);
        }

        public DateTimeOffset NextRequest
        {
            get => _repository.Get<DateTimeOffset>(nameof(NextRequest));
            set => _repository.Set<DateTimeOffset>(nameof(NextRequest), value);
        }

        public DateTimeOffset LastRequest
        {
            get => _repository.Get<DateTimeOffset>(nameof(LastRequest));
            set => _repository.Set<DateTimeOffset>(nameof(LastRequest), value);
        }

        public double FgPhotoScale
        {
            get
            {
                var value = _repository.Get<double>(nameof(FgPhotoScale));
                return value > 0 && value <= 1 ? value : 0.65;
            }
            set => _repository.Set<double>(nameof(FgPhotoScale), value);
        }

        public double FgPhotoXPos
        {
            get
            {
                var value = _repository.Get<double>(nameof(FgPhotoXPos));
                return value >= 0 && value <= 1 ? value : 0.5;
            }
            set => _repository.Set<double>(nameof(FgPhotoXPos), value);
        }

        public double FgPhotoYPos
        {
            get
            {
                var value = _repository.Get<double>(nameof(FgPhotoYPos));
                return value >= 0 && value <= 1 ? value : 0.5;
            }
            set => _repository.Set<double>(nameof(FgPhotoYPos), value);
        }

        public string FgPhotoOrientation
        {
            get
            {
                var value = _repository.Get<string>(nameof(FgPhotoOrientation));
                return value == "Landscape" ? "Landscape" : "Portrait";
            }
            set => _repository.Set<string>(nameof(FgPhotoOrientation), value);
        }

        public bool PhotoLayoutSaved
        {
            get => _repository.Get<bool>(nameof(PhotoLayoutSaved));
            set => _repository.Set(nameof(PhotoLayoutSaved), value);
        }

        public int PhotoLayoutVersion
        {
            get => _repository.Get<int>(nameof(PhotoLayoutVersion));
            set => _repository.Set(nameof(PhotoLayoutVersion), value);
        }

        public List<LayoutTextField> LayoutTextFields
        {
            get => _repository.Get<List<LayoutTextField>>(nameof(LayoutTextFields)) ?? new List<LayoutTextField>();
            set => _repository.Set(nameof(LayoutTextFields), value);
        }

        public float FgTopOffset
        {
            get { 
                var value =_repository.Get<float>(nameof(FgTopOffset)); 
                if (value == 0f)
                {
                    return 260f;
                }
                return value;
            }
            set => _repository.Set<float>(nameof(FgTopOffset), value);
        }
        public float FgBottomOffset
        {
            get { 
                var value = _repository.Get<float>(nameof(FgBottomOffset)); 
                if (value == 0f)
                {
                    return 1250f;
                }
                return value;
            }
            set => _repository.Set<float>(nameof(FgBottomOffset), value);
        }
        public float FgLeftOffset
        {
            get { 
                var value = _repository.Get<float>(nameof(FgLeftOffset)); 
                if (value == 0f)
                {
                    return 80f;
                }
                return value;
            }
            set => _repository.Set<float>(nameof(FgLeftOffset), value);
        }

        public PrintSettings? PrintSettings
            {
            get => _repository.Get<PrintSettings>(nameof(PrintService.Service.PrintSettings));
            set => _repository.Set<PrintSettings>(nameof(PrintService.Service.PrintSettings), value);
        }

    }
}

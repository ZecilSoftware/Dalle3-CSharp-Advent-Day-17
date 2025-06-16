using Azure.AI.OpenAI;
using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using System.Net;

namespace Dalle3_CSharp_Advent
{
    public sealed partial class MainWindow : Window
    {
        private const string OPENAI_KEY = "";
        private const string SAVE_FOLER = "Advent DALLE";
        private Uri _currentImage;
        private string _currentPrompt;

        public MainWindow()
        {
            this.InitializeComponent();
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            WorkingState();
            
            try
            {
                var folder = await GetPicturesFolder();
                var destination = Path.Combine(folder, SAVE_FOLER, $"{HumanPrompt.Text}.png");
                var destination2 = Path.Combine(folder, SAVE_FOLER, $"{HumanPrompt.Text}.txt");

                using (var client = new WebClient())
                {
                    client.DownloadFile(_currentImage, destination);
                }

                using (StreamWriter outputFile = new StreamWriter(destination2, false))
                {
                    outputFile.WriteLine(_currentPrompt);
                }

                SaveNotification.Title = "Image Saved";
                SaveNotification.Subtitle = destination;
                SaveNotification.IsOpen = true;
            }
            catch (Exception ex)
            {
                ShowErrorNotification("Error Saving Image", 
                    $"An error occurred while saving the image: {ex.Message}");
            }
            finally
            {
                FinishedState();
            }
        }

        private async void GenerateImage_Click(object sender, RoutedEventArgs e)
        {
            GeneratedImage.Source = null;
            WorkingState();

            try
            {
                // Validate API key before making any calls
                if (string.IsNullOrWhiteSpace(OPENAI_KEY))
                {
                    ShowErrorNotification("OpenAI API Key Missing", 
                        "Please add your OpenAI API key to the OPENAI_KEY constant in MainWindow.xaml.cs. " +
                        "Visit https://platform.openai.com/ to get your API key.");
                    return;
                }

                _currentPrompt = await GeneratePrompt(HumanPrompt.Text);

                ShowPrompt(_currentPrompt);
                var image = await GenerateImage(_currentPrompt);
                HidePrompt();

                GeneratedImage.Source = image;

                Save.IsEnabled = true;
            }
            catch (Exception ex)
            {
                HidePrompt();
                ShowErrorNotification("Error Generating Image", 
                    $"An error occurred while generating the image: {ex.Message}");
            }
            finally
            {
                FinishedState();
            }
        }

        private static async Task<string> GeneratePrompt(string userPrompt)
        {
            try
            {
                OpenAIClient client = new(OPENAI_KEY);

                var responseCompletion = await client.GetChatCompletionsAsync(
                    new ChatCompletionsOptions()
                    {
                        ChoiceCount = 1,
                        Temperature = 1,
                        MaxTokens = 256,                    
                        DeploymentName = "gpt-4",
                        Messages = {
                            new ChatRequestSystemMessage("Create a prompt for Dall-e that will generate a beautiful Christmas scene using the following text for inspiration:"),
                            new ChatRequestUserMessage(userPrompt),
                        },
                    });

                return responseCompletion.Value.Choices[0].Message.Content;
            }
            catch (Exception ex) when (ex.Message.Contains("Unauthorized") || ex.Message.Contains("401") || ex.Message.Contains("authentication"))
            {
                throw new Exception("Invalid OpenAI API key. Please check your API key and ensure it's valid.");
            }
            catch (Exception ex) when (ex.Message.Contains("quota") || ex.Message.Contains("limit"))
            {
                throw new Exception("OpenAI API quota exceeded. Please check your usage limits or billing information.");
            }
            catch (Exception ex)
            {
                throw new Exception($"OpenAI API error while generating prompt: {ex.Message}");
            }
        }

        private async Task<BitmapImage> GenerateImage(String prompt)
        {
            try
            {
                OpenAIClient client = new(OPENAI_KEY);

                var responseImages = await client.GetImageGenerationsAsync(
                    new ImageGenerationOptions()
                    {
                        ImageCount = 1,
                        Prompt = prompt,
                        Size = ImageSize.Size1792x1024,
                        DeploymentName = "dall-e-3"
                    });

                _currentImage = responseImages.Value.Data[0].Url;
                return new BitmapImage(_currentImage);
            }
            catch (Exception ex) when (ex.Message.Contains("Unauthorized") || ex.Message.Contains("401") || ex.Message.Contains("authentication"))
            {
                throw new Exception("Invalid OpenAI API key. Please check your API key and ensure it's valid.");
            }
            catch (Exception ex) when (ex.Message.Contains("quota") || ex.Message.Contains("limit"))
            {
                throw new Exception("OpenAI API quota exceeded. Please check your usage limits or billing information.");
            }
            catch (Exception ex) when (ex.Message.Contains("content_policy") || ex.Message.Contains("policy"))
            {
                throw new Exception("The generated prompt violates OpenAI's content policy. Please try a different input.");
            }
            catch (Exception ex)
            {
                throw new Exception($"OpenAI API error while generating image: {ex.Message}");
            }
        }

        private void ShowPrompt(string prompt)
        {
            GeneratedPrompt.Text = prompt;
            GeneratedPrompt.Visibility = Visibility.Visible;
        }

        private void HidePrompt()
        {
            GeneratedPrompt.Visibility = Visibility.Collapsed;
        }

        private void WorkingState()
        {
            Save.IsEnabled = false;
            Generate.IsEnabled = false;
            ProgressIndicator.IsActive = true;
        }

        private void FinishedState()
        {
            ProgressIndicator.IsActive = false;
            Generate.IsEnabled = true;
        }

        private void ShowErrorNotification(string title, string message)
        {
            SaveNotification.Title = title;
            SaveNotification.Subtitle = message;
            SaveNotification.IsOpen = true;
        }

        private static async Task<string> GetPicturesFolder()
        {
            var myPictures = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Pictures);
            Directory.CreateDirectory(Path.Combine(myPictures.SaveFolder.Path, SAVE_FOLER));
            return myPictures.SaveFolder.Path;
        }
    }
}
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.OS;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Engine;
using Terraria.ModLoader.UI;
using Terraria.Social;
using Terraria.UI.Chat;
using ReLogic.Content;
using Terraria.UI.Gamepad;

namespace Terraria;

public partial class Main
{
	public static int soundError;
	public static int ambientError;
	public static bool mouseMiddle;
	public static bool mouseXButton1;
	public static bool mouseXButton2;
	public static bool mouseMiddleRelease;
	public static bool mouseXButton1Release;
	public static bool mouseXButton2Release;
	public static Point16 trashSlotOffset;
	public static bool hidePlayerCraftingMenu;
	public static bool showServerConsole;
	public static bool Support8K = true; // provides an option to disable 8k (but leave 4k)
	public static double desiredWorldEventsUpdateRate = 1; // dictates the speed at which world events (falling stars, fairy spawns, sandstorms, etc.) can change/happen
	/// <summary>
	/// Representation that dictates the actual amount of "world event updates" that happen in a given GAME tick. This number increases/decreases in direct tandem with
	/// <seealso cref="desiredWorldEventsUpdateRate"/>.
	/// </summary>
	public static int worldEventUpdates;
	private double _partialWorldEventUpdates = 0f;

	public static List<TitleLinkButton> tModLoaderTitleLinks = new List<TitleLinkButton>();

	/// <summary>
	/// A color that cycles through the colors like Rainbow Brick does.
	/// </summary>
	public static Color DiscoColor => new Color(DiscoR, DiscoG, DiscoB);
	/// <summary>
	/// The typical pulsing white color used for much of the text shown in-game.
	/// </summary>
	public static Color MouseTextColorReal => new Color(mouseTextColor / 255f, mouseTextColor / 255f, mouseTextColor / 255f, mouseTextColor / 255f);
	public static bool PlayerLoaded => CurrentFrameFlags.ActivePlayersCount > 0;

	// Used to prevent situations where when going from borderless Fullscreen display to windowed display the width and height don't get resized to be able to access key window functions
	// Does not effect resizing the window manually. May not be perfect, but will at least be sufficient to provide room to manually address this on the end user side
	// Magic constant comes from default windows border settings: ~ 1377 / 1440 and 1033 / 1080.
	private static int BorderedHeight(int height, bool state) => (int)(height * (state ? 1 : 0.95625));

	// Tracks whether the Stylist has had her hairstyle list updated for the current interaction.
	private static bool hairstylesUpdatedForThisInteraction; // TML: Track whether hairstyle cache needs refreshing for Stylist UI.

	private static Player _currentPlayerOverride;

	/// <summary>
	/// A replacement for `Main.LocalPlayer` which respects whichever player is currently running hooks on the main thread.
	/// This works in the player select screen, and in multiplayer (when other players are updating)
	/// </summary>
	public static Player CurrentPlayer => _currentPlayerOverride ?? LocalPlayer;

	/// <summary>
	/// Checks if a tile at the given coordinates counts towards tile coloring from the Spelunker buff, and is detected by various pets.
	/// </summary>
	public static bool IsTileSpelunkable(int tileX, int tileY)
	{
		Tile tile = Main.tile[tileX, tileY];
		return IsTileSpelunkable(tileX, tileY, tile.type, tile.frameX, tile.frameY);
	}

	/// <summary>
	/// Checks if a tile at the given coordinates counts towards tile coloring from the Biome Sight buff.
	/// </summary>
	public static bool IsTileBiomeSightable(int tileX, int tileY, ref Color sightColor)
	{
		Tile tile = Main.tile[tileX, tileY];
		return IsTileBiomeSightable(tileX, tileY, tile.type, tile.frameX, tile.frameY, ref sightColor);
	}

	public static void InfoDisplayPageHandler(int startX, ref string mouseText, out int startingDisplay, out int endingDisplay)
	{
		startingDisplay = 0;
		endingDisplay = InfoDisplayLoader.InfoDisplayCount;

		if (playerInventory && InfoDisplayLoader.ActiveDisplays() > 12) {
			startingDisplay = 12 * InfoDisplayLoader.InfoDisplayPage;

			if (InfoDisplayLoader.ActiveDisplays() - startingDisplay <= 12)
				endingDisplay = InfoDisplayLoader.ActiveDisplays();
			else
				endingDisplay = startingDisplay + 12;

			if (startingDisplay >= 8)
				startingDisplay += 1;

			endingDisplay += 1;

			Texture2D buttonTexture = UICommon.InfoDisplayPageArrowTexture.Value;
			bool hovering = false;

			GetInfoAccIconPosition(11, startX, out int X, out int Y);
			Vector2 buttonPosition = new Vector2(X, Y + 20);

			if ((float)mouseX >= buttonPosition.X && (float)mouseY >= buttonPosition.Y && (float)mouseX <= buttonPosition.X + (float)buttonTexture.Width && (float)mouseY <= buttonPosition.Y + (float)buttonTexture.Height && !PlayerInput.IgnoreMouseInterface) {
				hovering = true;
				player[myPlayer].mouseInterface = true;

				if (mouseLeft && mouseLeftRelease) {
					SoundEngine.PlaySound(12);
					mouseLeftRelease = false;

					if (InfoDisplayLoader.ActivePages() != InfoDisplayLoader.InfoDisplayPage + 1)
						InfoDisplayLoader.InfoDisplayPage += 1;
					else
						InfoDisplayLoader.InfoDisplayPage = 0;
				}

				if (!Main.mouseText) {
					mouseText = (InfoDisplayLoader.ActivePages() != InfoDisplayLoader.InfoDisplayPage + 1) ? Language.GetTextValue("tModLoader.NextInfoAccPage") : Language.GetTextValue("tModLoader.FirstInfoAccPage");
					Main.mouseText = true;
				}
			}

			spriteBatch.Draw(buttonTexture, buttonPosition, new Rectangle(0, 0, buttonTexture.Width, buttonTexture.Height), Color.White, 0f, default, 1f, SpriteEffects.None, 0f);

			if (hovering)
				spriteBatch.Draw(TextureAssets.InfoIcon[13].Value, buttonPosition - Vector2.One * 2f, null, OurFavoriteColor, 0f, default, 1f, SpriteEffects.None, 0f);

			hovering = false;
			GetInfoAccIconPosition(0, startX, out X, out int _);
			buttonPosition = new Vector2(X, Y + 20);

			if ((float)mouseX >= buttonPosition.X && (float)mouseY >= buttonPosition.Y && (float)mouseX <= buttonPosition.X + (float)buttonTexture.Width && (float)mouseY <= buttonPosition.Y + (float)buttonTexture.Height && !PlayerInput.IgnoreMouseInterface) {
				hovering = true;
				player[myPlayer].mouseInterface = true;

				if (mouseLeft && mouseLeftRelease) {
					SoundEngine.PlaySound(12);
					mouseLeftRelease = false;

					if (InfoDisplayLoader.InfoDisplayPage != 0)
						InfoDisplayLoader.InfoDisplayPage -= 1;
					else
						InfoDisplayLoader.InfoDisplayPage = InfoDisplayLoader.ActivePages() - 1;
				}

				if (!Main.mouseText) {
					mouseText = (InfoDisplayLoader.InfoDisplayPage != 0) ? Language.GetTextValue("tModLoader.PreviousInfoAccPage") : Language.GetTextValue("tModLoader.LastInfoAccPage");
					Main.mouseText = true;
				}
			}

			spriteBatch.Draw(buttonTexture, buttonPosition, new Rectangle(0, 0, buttonTexture.Width, buttonTexture.Height), Color.White, 0f, default, 1f, SpriteEffects.FlipHorizontally, 0f);

			if (hovering)
				spriteBatch.Draw(TextureAssets.InfoIcon[13].Value, buttonPosition - Vector2.One * 2f, null, OurFavoriteColor, 0f, default, 1f, SpriteEffects.None, 0f);
		}
	}
	
	public static void BuilderTogglePageHandler(int startY, int activeToggles, out bool moveDownForButton, out int startIndex, out int endIndex) {
		startIndex = 0;
		endIndex = activeToggles;
		moveDownForButton = false;
		string text = "";

		if (activeToggles > 12) {
			startIndex = 12 * BuilderToggleLoader.BuilderTogglePage;

			if (activeToggles - startIndex < 12)
				endIndex = activeToggles;
			else if (startIndex == 0)
				endIndex = startIndex + 12;
			else
				endIndex = startIndex + 11;

			Texture2D buttonTexture = UICommon.InfoDisplayPageArrowTexture.Value;
			bool hover = false;
			Vector2 buttonPosition = new Vector2(3, startY - 6f);

			if (BuilderToggleLoader.BuilderTogglePage != 0) {
				moveDownForButton = true;

				spriteBatch.Draw(buttonTexture, buttonPosition + new Vector2(0, 13f), new Rectangle(0, 0, buttonTexture.Width, buttonTexture.Height), Color.White, -(float)Math.PI / 2, default, 1f, SpriteEffects.None, 0f);
				if ((float)mouseX >= buttonPosition.X && (float)mouseY >= buttonPosition.Y && (float)mouseX <= buttonPosition.X + (float)buttonTexture.Width && (float)mouseY <= buttonPosition.Y + (float)buttonTexture.Height && !PlayerInput.IgnoreMouseInterface) {
					hover = true;

					player[myPlayer].mouseInterface = true;
					text = "Previous Page";
					mouseText = true;

					if (mouseLeft && mouseLeftRelease) {
						SoundEngine.PlaySound(SoundID.MenuTick);
						mouseLeftRelease = false;

						if (BuilderToggleLoader.BuilderTogglePage > 0)
							BuilderToggleLoader.BuilderTogglePage--;
					}

					spriteBatch.Draw(TextureAssets.InfoIcon[13].Value, buttonPosition + new Vector2(0, 17) - Vector2.One * 2f, null, OurFavoriteColor, -(float)Math.PI / 2, default, 1f, SpriteEffects.None, 0f);
				}
			}

			buttonPosition = new Vector2(3, startY + ((endIndex - startIndex) + (BuilderToggleLoader.BuilderTogglePage != 0).ToInt()) * 24f - 6f);

			if (BuilderToggleLoader.BuilderTogglePage != activeToggles / 12) {
				spriteBatch.Draw(buttonTexture, buttonPosition + new Vector2(0, 12f), new Rectangle(0, 0, buttonTexture.Width, buttonTexture.Height), Color.White, (float)Math.PI / 2, new Vector2(buttonTexture.Width, buttonTexture.Height), 1f, SpriteEffects.None, 0f);

				if ((float)mouseX >= buttonPosition.X && (float)mouseY >= buttonPosition.Y && (float)mouseX <= buttonPosition.X + (float)buttonTexture.Width && (float)mouseY <= buttonPosition.Y + (float)buttonTexture.Height && !PlayerInput.IgnoreMouseInterface) {
					hover = true;

					player[myPlayer].mouseInterface = true;
					text = "Next Page";
					mouseText = true;

					if (mouseLeft && mouseLeftRelease) {
						SoundEngine.PlaySound(SoundID.MenuTick);
						mouseLeftRelease = false;

						if (BuilderToggleLoader.BuilderTogglePage < activeToggles / 12)
							BuilderToggleLoader.BuilderTogglePage++;
					}

					spriteBatch.Draw(TextureAssets.InfoIcon[13].Value, buttonPosition + new Vector2(4, 12) - Vector2.One * 2f, null, OurFavoriteColor, (float)Math.PI / 2, new Vector2(buttonTexture.Width, buttonTexture.Height), 1f, SpriteEffects.None, 0f);
				}
			}

			if (mouseText && hover) {
				float colorByte = (float)mouseTextColor / 255f;
				Color textColor = new Color(colorByte, colorByte, colorByte);

				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.MouseText.Value, text, new Vector2(mouseX + 14, mouseY + 14), textColor, 0f, Vector2.Zero, Vector2.One);
				mouseText = false;
			}
		}
	}

	private void DrawBuilderAccToggles_Inner(Vector2 start)
	{
		Player player = Main.player[myPlayer];
		int[] builderAccStatus = player.builderAccStatus;
		List<BuilderToggle> activeToggles = BuilderToggleLoader.ActiveBuilderTogglesList();
		bool shiftHotbarLock = activeToggles.Count / 12 != BuilderToggleLoader.BuilderTogglePage || activeToggles.Count % 12 >= 10;
		Vector2 startPosition = start - new Vector2(0, shiftHotbarLock ? 42 : 21);
		string text = "";

		BuilderTogglePageHandler((int)startPosition.Y, activeToggles.Count, out bool moveDownForButton, out int startIndex, out int endIndex);
		for (int i = startIndex; i < endIndex; i++) {
			BuilderToggle builderToggle = activeToggles[i];

			Texture2D texture = ModContent.Request<Texture2D>(builderToggle.Texture).Value;
			Rectangle rectangle = new Rectangle(0, 0, texture.Width, texture.Height);
			Color color = builderToggle.DisplayColorTexture();

			Vector2 position = startPosition + new Vector2(0, moveDownForButton ? 24 : 0) + new Vector2(0, (i % 12) * 24);
			text = builderToggle.DisplayValue();
			int numberOfStates = builderToggle.NumberOfStates;
			int toggleType = builderToggle.Type;

			/*
			BuilderToggleLoader.ModifyNumberOfStates(builderToggle, ref numberOfStates);
			BuilderToggleLoader.ModifyDisplayValue(builderToggle, ref text);
			*/

			bool hover = Utils.CenteredRectangle(position, new Vector2(14f)).Contains(MouseScreen.ToPoint()) && !PlayerInput.IgnoreMouseInterface;
			bool click = hover && mouseLeft && mouseLeftRelease;

			if (toggleType == BuilderToggle.BlockSwap.Type || toggleType == BuilderToggle.TorchBiome.Type) {
				if (toggleType == BuilderToggle.BlockSwap.Type)
					rectangle = texture.Frame(3, 1, builderToggle.CurrentState != 0 ? 1 : 0);
				else
					rectangle = texture.Frame(4, 1, builderToggle.CurrentState == 0 ? 1 : 0);

				position += new Vector2(1, 0);
			}
			else
				rectangle = builderToggle.Type < 10 ? new Rectangle(builderToggle.Type * 16, 16, 14, 14) : rectangle;

			/*
			BuilderToggleLoader.ModifyDisplayColor(builderToggle, ref color);
			BuilderToggleLoader.ModifyDisplayTexture(builderToggle, ref texture, ref rectangle);
			*/

			spriteBatch.Draw(texture, position, rectangle, color, 0f, rectangle.Size() / 2f, 1f, SpriteEffects.None, 0f);

			if (hover) {
				player.mouseInterface = true;
				mouseText = true;

				if (toggleType != BuilderToggle.BlockSwap.Type && toggleType != BuilderToggle.TorchBiome.Type) {
					Asset<Texture2D> iconHover = ModContent.Request<Texture2D>(builderToggle.HoverTexture);
					spriteBatch.Draw(iconHover.Value, position, null, OurFavoriteColor, 0f, iconHover.Value.Size() / 2f, 1f, SpriteEffects.None, 0f);
				}
				else if (toggleType == BuilderToggle.BlockSwap.Type)
					spriteBatch.Draw(texture, position, texture.Frame(3, 1, 2), OurFavoriteColor, 0f, rectangle.Size() / 2f, 0.9f, SpriteEffects.None, 0f);
				else if (toggleType == BuilderToggle.TorchBiome.Type)
					spriteBatch.Draw(texture, position, texture.Frame(4, 1, builderToggle.CurrentState == 0 ? 3 : 2), OurFavoriteColor, 0f, rectangle.Size() / 2f, 0.9f, SpriteEffects.None, 0f);
			}

			if (click) {
				builderAccStatus[toggleType] = (builderAccStatus[toggleType] + 1) % numberOfStates;
				SoundEngine.PlaySound((toggleType == BuilderToggle.BlockSwap.Type || toggleType == BuilderToggle.TorchBiome.Type) ? SoundID.Unlock : SoundID.MenuTick);
				mouseLeftRelease = false;
			}


			UILinkPointNavigator.SetPosition(6000 + i % 12, position + rectangle.Size() * 0.15f);

			if (mouseText && hover && HoverItem.type <= 0) {
				float colorByte = (float)mouseTextColor / 255f;
				Color textColor = new Color(colorByte, colorByte, colorByte);

				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.MouseText.Value, text, new Vector2(mouseX + 14, mouseY + 14), textColor, 0f, Vector2.Zero, Vector2.One);
				mouseText = false;
			}
		}

		UILinkPointNavigator.Shortcuts.BUILDERACCCOUNT = (endIndex - startIndex);
	}

	//Mirrors code used in UpdateTime
	/// <summary>
	/// Syncs rain state if <see cref="StartRain"/> or <see cref="StopRain"/> were called in the same tick and caused a change to <seealso cref="maxRaining"/>.
	/// <br>Can be called on any side, but only the server will actually sync it.</br>
	/// </summary>
	public static void SyncRain()
	{
		if (maxRaining != oldMaxRaining) {
			if (netMode == 2)
				NetMessage.SendData(7);

			oldMaxRaining = maxRaining;
		}
	}

	public ref struct CurrentPlayerOverride
	{
		private Player _prevPlayer;

		public CurrentPlayerOverride(Player player)
		{
			_prevPlayer = _currentPlayerOverride;
			_currentPlayerOverride = player;
		}

		public void Dispose()
		{
			_currentPlayerOverride = _prevPlayer;
		}
	}

	internal void InitTMLContentManager()
	{
		if (dedServ) {
			return;
		}

		string vanillaContentFolder;
		if (SocialAPI.Mode == SocialMode.Steam) {
			vanillaContentFolder = Path.Combine(Steam.GetSteamTerrariaInstallDir(), "Content");
		}
		else {
			vanillaContentFolder = Platform.IsOSX ? "../Terraria/Terraria.app/Contents/Resources/Content" : "../Terraria/Content"; // Side-by-Side Manual Install

			if (!Directory.Exists(vanillaContentFolder)) {
				vanillaContentFolder = Platform.IsOSX ? "../Terraria.app/Contents/Resources/Content" : "../Content"; // Nested Manual Install
			}
			Logging.tML.Info("Content folder of Terraria GOG Install Location assumed to be: " + Path.GetFullPath(vanillaContentFolder));
		}

		if (!Directory.Exists(vanillaContentFolder)) {
			ErrorReporting.FatalExit(Language.GetTextValue("tModLoader.ContentFolderNotFound"));
		}

		// Canary file, ensures that Terraria has updated to at least the version this tModLoader was built for. Alternate check to BuildID check in TerrariaSteamClient for non-Steam launches 
		if (!File.Exists(Path.Combine(vanillaContentFolder, "Images", "Projectile_981.xnb"))) {
			ErrorReporting.FatalExit(Language.GetTextValue("tModLoader.TerrariaOutOfDateMessage"));
		}


		TMLContentManager localOverrideContentManager = null;
		if (Directory.Exists(Path.Combine("Content", "Images")))
			localOverrideContentManager = new TMLContentManager(Content.ServiceProvider, "Content", null);

		base.Content = new TMLContentManager(Content.ServiceProvider, vanillaContentFolder, localOverrideContentManager);
	}
	
	private static void DrawtModLoaderSocialMediaButtons(Microsoft.Xna.Framework.Color menuColor, float upBump)
	{
		List<TitleLinkButton> titleLinks = tModLoaderTitleLinks;
		Vector2 anchorPosition = new Vector2(18f, (float)(screenHeight - 26 - 22) - upBump);
		for (int i = 0; i < titleLinks.Count; i++) {
			titleLinks[i].Draw(spriteBatch, anchorPosition);
			anchorPosition.X += 30f;
		}
	}

	/// <summary>
	/// Wait for an action to be performed on the main thread.
	/// </summary>
	/// <param name="action"></param>
	public static Task RunOnMainThread(Action action)
	{
		var tcs = new TaskCompletionSource();

		QueueMainThreadAction(() => {
			action();
			tcs.SetResult();
		});

		return tcs.Task;
	}

	/// <summary>
	/// Wait for an action to be performed on the main thread.
	/// </summary>
	/// <param name="func"></param>
	public static Task<T> RunOnMainThread<T>(Func<T> func)
	{
		var tcs = new TaskCompletionSource<T>();
		QueueMainThreadAction(() => tcs.SetResult(func()));
		return tcs.Task;
	}

	public static void AddSignalTraps()
	{
		static void Handle(PosixSignalContext ctx) {
			ctx.Cancel = true;
			Logging.tML.Info($"Signal {ctx.Signal}, Closing Server...");
			Netplay.Disconnect = true;
		}

		PosixSignalRegistration.Create(PosixSignal.SIGINT, Handle);
		PosixSignalRegistration.Create(PosixSignal.SIGTERM, Handle);
	}
}

using System;
using System.IO;
using UnityEditorInternal;
using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using Projeny.Internal;
using System.Linq;

namespace Projeny
{
    public class PackageManagerWindow : EditorWindow
    {
        PackageManagerModel _model;

        bool _hasInitialized;

        [NonSerialized]
        PackageManagerView _view;

        [NonSerialized]
        EventManager _eventManager;

        [NonSerialized]
        AsyncProcessor _asyncProcessor;

        [NonSerialized]
        bool _isDisplayingError;

        void OnEnable()
        {
            if (!_hasInitialized)
            {
                _hasInitialized = true;

                _model = new PackageManagerModel();

                Log.Trace("STEVETODO");
                //AddBackgroundTask(RefreshAll(), "Refreshing Packages");
            }

            Assert.IsNotNull(_model);
            Assert.IsNull(_view);
            Assert.IsNull(_eventManager);
            Assert.IsNull(_asyncProcessor);

            _asyncProcessor = new AsyncProcessor();
            _eventManager = new EventManager();

            _view = new PackageManagerView();

            ObserveViewEvents();
        }

        void ObserveViewEvents()
        {
            // Use EventQueueMode.LatestOnly to ensure we don't execute anything during the OnGUI event
            // This is important since OnGUI is called in multiple passes and we have to ensure that the same
            // controls are rendered each pass
            _view.ClickedProjectType += _eventManager.Add<ProjectConfigTypes>(OnClickedProjectType, EventQueueMode.LatestOnly);
            _view.ClickedRefreshReleaseList += _eventManager.Add(OnClickedRefreshReleaseList, EventQueueMode.LatestOnly);
            _view.ClickedRefreshPackages += _eventManager.Add(OnClickedRefreshPackages, EventQueueMode.LatestOnly);
            _view.ClickedCreateNewPackage += _eventManager.Add(OnClickedCreateNewPackage, EventQueueMode.LatestOnly);
            _view.ClickedProjectApplyButton += _eventManager.Add(OnClickedProjectApplyButton, EventQueueMode.LatestOnly);
            _view.ClickedProjectRevertButton += _eventManager.Add(OnClickedProjectRevertButton, EventQueueMode.LatestOnly);
            _view.ClickedProjectSaveButton += _eventManager.Add(OnClickedProjectSaveButton, EventQueueMode.LatestOnly);
            _view.ClickedProjectEditButton += _eventManager.Add(OnClickedProjectEditButton, EventQueueMode.LatestOnly);
            _view.ClickedReleasesSortMenu += _eventManager.Add<Rect>(OnClickedReleasesSortMenu, EventQueueMode.LatestOnly);
            _view.ContextMenuOpened += _eventManager.Add<DraggableList>(OnContextMenuOpened, EventQueueMode.LatestOnly);

            _view.DraggedDroppedListEntries += _eventManager.Add<ListTypes, ListTypes, List<DraggableListEntry>>(OnDraggedDroppedListEntries, EventQueueMode.LatestOnly);

            _model.PluginItemsChanged += _eventManager.Add(OnDisplayValuesDirty, EventQueueMode.LatestOnly);
            _model.AssetItemsChanged += _eventManager.Add(OnDisplayValuesDirty, EventQueueMode.LatestOnly);
            _model.PackagesChanged += _eventManager.Add(OnDisplayValuesDirty, EventQueueMode.LatestOnly);
        }

        void UnobserveViewEvents()
        {
            _view.ClickedProjectType -= _eventManager.Remove<ProjectConfigTypes>(OnClickedProjectType);
            _view.ClickedRefreshReleaseList -= _eventManager.Remove(OnClickedRefreshReleaseList);
            _view.ClickedRefreshPackages -= _eventManager.Remove(OnClickedRefreshPackages);
            _view.ClickedCreateNewPackage -= _eventManager.Remove(OnClickedCreateNewPackage);
            _view.ClickedProjectApplyButton -= _eventManager.Remove(OnClickedProjectApplyButton);
            _view.ClickedProjectRevertButton -= _eventManager.Remove(OnClickedProjectRevertButton);
            _view.ClickedProjectSaveButton -= _eventManager.Remove(OnClickedProjectSaveButton);
            _view.ClickedProjectEditButton -= _eventManager.Remove(OnClickedProjectEditButton);
            _view.ClickedReleasesSortMenu -= _eventManager.Remove<Rect>(OnClickedReleasesSortMenu);
            _view.ContextMenuOpened -= _eventManager.Remove<DraggableList>(OnContextMenuOpened);

            _view.DraggedDroppedListEntries -= _eventManager.Remove<ListTypes, ListTypes, List<DraggableListEntry>>(OnDraggedDroppedListEntries);

            _model.PluginItemsChanged -= _eventManager.Remove(OnDisplayValuesDirty);
            _model.AssetItemsChanged -= _eventManager.Remove(OnDisplayValuesDirty);
            _model.PackagesChanged -= _eventManager.Remove(OnDisplayValuesDirty);

            _eventManager.AssertIsEmpty();
        }

        void OnDraggedDroppedListEntries(ListTypes sourceType, ListTypes dropType, List<DraggableListEntry> entries)
        {
            foreach (var entry in entries)
            {
                OnDraggedDroppedListEntry(sourceType, dropType, entry);
            }
        }

        void OnDraggedDroppedListEntry(ListTypes sourceType, ListTypes dropType, DraggableListEntry entry)
        {
            switch (dropType)
            {
                case ListTypes.Package:
                {
                    switch (sourceType)
                    {
                        case ListTypes.PluginItem:
                        {
                            var name = (string)entry.Tag;
                            _model.RemovePluginItem(name);
                            break;
                        }
                        case ListTypes.AssetItem:
                        {
                            var name = (string)entry.Tag;
                            _model.RemoveAssetItem(name);
                            break;
                        }
                        case ListTypes.Release:
                        {
                            Assert.Throw("TODO");
                            //AddBackgroundTask(InstallReleasesAsync(data.Entries.Select(x => (ReleaseInfo)x.Tag).ToList()), "Installing Releases");
                            break;
                        }
                        default:
                        {
                            Assert.Throw();
                            break;
                        }
                    }

                    break;
                }
                case ListTypes.PluginItem:
                {
                    switch (sourceType)
                    {
                        case ListTypes.AssetItem:
                        {
                            var name = (string)entry.Tag;
                            _model.RemoveAssetItem(name);
                            _model.AddPluginItem(name);

                            break;
                        }
                        case ListTypes.PluginItem:
                        {
                            // Do nothing
                            break;
                        }
                        case ListTypes.Package:
                        {
                            var info = (PackageInfo)entry.Tag;

                            if (!_model.HasPluginItem(info.Name))
                            {
                                if (_model.HasAssetItem(info.Name))
                                {
                                    _model.RemoveAssetItem(info.Name);
                                }

                                _model.AddPluginItem(info.Name);
                            }

                            break;
                        }
                        default:
                        {
                            Assert.Throw("TODO");
                            break;
                        }
                    }

                    break;
                }
                case ListTypes.AssetItem:
                {
                    switch (sourceType)
                    {
                        case ListTypes.AssetItem:
                        {
                            // Do nothing
                            break;
                        }
                        case ListTypes.PluginItem:
                        {
                            var name = (string)entry.Tag;

                            _model.RemovePluginItem(name);
                            _model.AddAssetItem(name);
                            break;
                        }
                        case ListTypes.Package:
                        {
                            var info = (PackageInfo)entry.Tag;

                            if (!_model.HasAssetItem(info.Name))
                            {
                                if (_model.HasPluginItem(info.Name))
                                {
                                    _model.RemovePluginItem(info.Name);
                                }

                                _model.AddAssetItem(info.Name);
                            }

                            break;
                        }
                        default:
                        {
                            Assert.Throw();
                            break;
                        }
                    }

                    break;
                }
                case ListTypes.Release:
                {
                    // Nothing can drag here
                    break;
                }
                default:
                {
                    Assert.Throw();
                    break;
                }
            }
        }

        void OnDisplayValuesDirty()
        {
            _view.SetPluginItems(_model.PluginItems
                .Select(x => CreateListItemForProjectItem(x)).ToList());

            _view.SetAssetItems(_model.AssetItems
                .Select(x => CreateListItemForProjectItem(x)).ToList());

            _view.SetPackages(_model.Packages
                .Select(x => CreateListItem(x)).ToList());
        }

        PackageManagerView.ListItemData CreateListItemForProjectItem(string name)
        {
            string caption;

            if (_model.ViewState == PackageManagerViewStates.PackagesAndProject)
            {
                caption = ImguiUtil.WrapWithColor(name, _view.Skin.Theme.DraggableItemAlreadyAddedColor);
            }
            else
            {
                // this isn't always the case since it can be rendered when interpolating
                //Assert.That(_viewState == PackageManagerViewStates.Project);
                caption = name;
            }

            return new PackageManagerView.ListItemData()
            {
                Caption = caption,
                Tag = name
            };
        }

        PackageManagerView.ListItemData CreateListItem(PackageInfo info)
        {
            if (_model.ViewState == PackageManagerViewStates.ReleasesAndPackages)
            {
                Assert.Throw("TODO");
                //if (info.InstallInfo != null && info.InstallInfo.ReleaseInfo != null)
                //{
                    //var releaseInfo = info.InstallInfo.ReleaseInfo;

                    //var displayValue = "{0} ({1}{2})".Fmt(
                        //info.Name,
                        //WrapWithColor(releaseInfo.Name, Skin.Theme.DraggableItemAlreadyAddedColor),
                        //string.IsNullOrEmpty(releaseInfo.Version) ? "" : WrapWithColor(" v" + releaseInfo.Version, Skin.Theme.VersionColor));

                    //GUI.Label(rect, displayValue, Skin.ItemTextStyle);
                //}
                //else
                //{
                    //DrawListItem(rect, info.Name);
                //}
            }

            // this isn't always the case since it can be rendered when interpolating
            //Assert.IsEqual(_model.ViewState, PackageManagerViewStates.PackagesAndProject);

            string caption;

            if (_model.IsPackageAddedToProject(info.Name))
            {
                caption = ImguiUtil.WrapWithColor(
                    info.Name, _view.Skin.Theme.DraggableItemAlreadyAddedColor);
            }
            else
            {
                caption = info.Name;
            }

            return new PackageManagerView.ListItemData()
            {
                Caption = caption,
                Tag = info
            };
        }

        void OnDisable()
        {
            UnobserveViewEvents();
        }

        public void OnClickedReleasesSortMenu(Rect buttonRect)
        {
            ShowReleasesSortMenu(new Vector2(buttonRect.xMin, buttonRect.yMax));
        }

        void ShowReleasesSortMenu(Vector2 startPos)
        {
            GenericMenu contextMenu = new GenericMenu();

            contextMenu.AddItem(
                new GUIContent("Order By Name"),
                _model.ReleasesSortMethod == ReleasesSortMethod.Name,
                () => ChangeReleaseSortMethod(ReleasesSortMethod.Name));

            contextMenu.AddItem(
                new GUIContent("Order By Size"),
                _model.ReleasesSortMethod == ReleasesSortMethod.Size,
                () => ChangeReleaseSortMethod(ReleasesSortMethod.Size));

            contextMenu.AddItem(
                new GUIContent("Order By Publish Date"),
                _model.ReleasesSortMethod == ReleasesSortMethod.PublishDate,
                () => ChangeReleaseSortMethod(ReleasesSortMethod.PublishDate));

            contextMenu.DropDown(new Rect(startPos.x, startPos.y, 0, 0));
        }

        void ChangeReleaseSortMethod(ReleasesSortMethod sortMethod)
        {
            _model.ReleasesSortMethod = sortMethod;
            Assert.Throw("TODO");
            //_releasesList.ForceSort();
        }

        public void OnClickedProjectEditButton()
        {
            var configPath = ProjenyEditorUtil.GetProjectConfigPath(_model.ProjectConfigType);
            InternalEditorUtility.OpenFileAtLineExternal(configPath, 1);
        }

        public void OnClickedProjectSaveButton()
        {
            OverwriteConfig();
        }

        public void OnClickedProjectRevertButton()
        {
            RefreshProject();
        }

        public void OnClickedProjectApplyButton()
        {
            OverwriteConfig();
            AddBackgroundTask(ProcessUpmCommand("Updating directory links", UpmHelper.UpdateLinksAsync()), "Updating Links");
        }

        public void OnClickedCreateNewPackage()
        {
            AddBackgroundTask(CreateNewPackageAsync(), "Creating New Package");
        }

        public void OnClickedRefreshPackages()
        {
            AddBackgroundTask(RefreshPackagesAsync(), "Refreshing Packages");
        }

        public void OnClickedRefreshReleaseList()
        {
            AddBackgroundTask(RefreshReleasesAsync(), "Refreshing Release List");
        }

        public void OnClickedProjectType(ProjectConfigTypes desiredConfigType)
        {
            AddBackgroundTask(TryChangeProjectType(desiredConfigType));
        }

        IEnumerator RefreshAll()
        {
            RefreshProject();
            yield return RefreshPackagesAsync();
            yield return RefreshReleasesAsync();
        }

        void Update()
        {
            _eventManager.Flush();
            _asyncProcessor.Tick();
            //UpdateBackgroundTask();

            _view.IsBlocked = _asyncProcessor.IsBlocking;

            //if (_backgroundTaskInfo != null)
            //{
                //_view.IsBlocked = true;
                //_view.BlockedStatusMessage = _backgroundTaskInfo.StatusMessage;
                //_view.BlockedStatusTitle = _backgroundTaskInfo.StatusTitle;
            //}
            //else
            //{
                //_view.IsBlocked = false;
                //_view.BlockedStatusMessage = null;
                //_view.BlockedStatusTitle = null;
            //}

            _view.Update();

            // Doesn't seem worth trying to detect changes, just redraw every frame
            Repaint();
        }

        //void UpdateBackgroundTask()
        //{
            //if (_backgroundTaskInfo == null)
            //{
                //return;
            //}

            //try
            //{
                //// NOTE: Do not assume a constant frame rate here
                //// (When we aren't in focus this gets updated less often)
                //if (!_backgroundTaskInfo.CoRoutine.Pump())
                //{
                    //_backgroundTaskInfo = null;
                //}
            //}
            //catch (CoRoutineException e)
            //{
                //_backgroundTaskInfo = null;

                //// If possible, display this as a popup
                //// Otherwise it will still be visible in the console so that's fine
                //if (e.InnerException != null)
                //{
                    //AddBackgroundTask(
                        //_view.DisplayError(e.InnerException.Message));
                //}

                //throw;
            //}
            //catch (Exception e)
            //{
                //_backgroundTaskInfo = null;

                //AddBackgroundTask(
                    //_view.DisplayError(e.Message));
                //throw;
            //}
        //}

        public void OnContextMenuOpened(DraggableList sourceList)
        {
            // Move this to view?

            //var listType = ClassifyList(sourceList);

            //GenericMenu contextMenu = new GenericMenu();

            //switch (listType)
            //{
                //case ListTypes.Release:
                //{
                    //bool hasLocalPath = false;
                    //bool hasAssetStoreLink = false;

                    //var singleInfo = _selected.OnlyOrDefault();

                    //if (singleInfo != null)
                    //{
                        //var info = (ReleaseInfo)singleInfo.Tag;

                        //hasLocalPath = info.LocalPath != null && File.Exists(info.LocalPath);

                        //hasAssetStoreLink = info.AssetStoreInfo != null && !string.IsNullOrEmpty(info.AssetStoreInfo.LinkId);
                    //}

                    //contextMenu.AddOptionalItem(hasLocalPath, new GUIContent("Open Folder"), false, OpenReleaseFolderForSelected);

                    //contextMenu.AddOptionalItem(singleInfo != null, new GUIContent("More Info..."), false, OpenMoreInfoPopupForSelected);

                    //contextMenu.AddOptionalItem(hasAssetStoreLink, new GUIContent("Open In Asset Store"), false, OpenSelectedInAssetStore);
                    //break;
                //}
                //case ListTypes.Package:
                //{
                    //contextMenu.AddOptionalItem(!_selected.IsEmpty(), new GUIContent("Delete"), false, DeleteSelected);
                    //contextMenu.AddOptionalItem(_selected.Count == 1, new GUIContent("Rename"), false, RenameSelectedPackage);
                    //contextMenu.AddOptionalItem(_selected.Count == 1, new GUIContent("Open Folder"), false, OpenPackageFolderForSelected);
                    //contextMenu.AddOptionalItem(_selected.Count == 1 && HasPackageYaml((PackageInfo)_selected.Single().Tag), new GUIContent("Edit " + ProjenyEditorUtil.PackageConfigFileName), false, EditPackageYamlSelected);
                    //break;
                //}
                //case ListTypes.AssetItem:
                //case ListTypes.PluginItem:
                //{
                    //contextMenu.AddItem(new GUIContent("Remove"), false, DeleteSelected);
                    //contextMenu.AddOptionalItem(_selected.Count == 1 && HasFolderWithPackageName(_selected.Single().Name), new GUIContent("Select in Project Tab"), false, ShowSelectedInProjectTab);
                    //break;
                //}
                //default:
                //{
                    //Assert.Throw();
                    //break;
                //}
            //}

            //contextMenu.ShowAsContext();
        }

        bool HasFolderWithPackageName(string name)
        {
            return TryGetAssetForPackageName(name) != null;
        }

        UnityEngine.Object TryGetAssetForPackageName(string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/" + name);

            if (asset == null)
            {
                asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Plugins/" + name);
            }

            return asset;
        }

        void ShowSelectedInProjectTab()
        {
            Assert.Throw("TODO");
            //Assert.That(_selected.Select(x => ClassifyList(x.ListOwner)).All(x => x == ListTypes.PluginItem || x == ListTypes.AssetItem));
            //Assert.IsEqual(_selected.Count, 1);

            //var name = _selected.Single().Name;

            //var asset = TryGetAssetForPackageName(name);

            //if (asset == null)
            //{
                //AddBackgroundTask(DisplayError("Could not find package '{0}' in project".Fmt(name)));
            //}
            //else
            //{
                //Selection.activeObject = asset;
            //}
        }

        void OpenSelectedInAssetStore()
        {
            Assert.Throw("TODO");
            //Assert.IsEqual(_selected.Count, 1);

            //var entry = _selected.Single();

            //Assert.IsEqual(ClassifyList(entry.ListOwner), ListTypes.Release);

            //var info = (ReleaseInfo)entry.Tag;
            //var assetStoreInfo = info.AssetStoreInfo;

            //Assert.IsNotNull(assetStoreInfo);

            //var fullUrl = "https://www.assetstore.unity3d.com/#/{0}/{1}".Fmt(assetStoreInfo.LinkType, assetStoreInfo.LinkId);
            //Application.OpenURL(fullUrl);
        }

        void OpenMoreInfoPopupForSelected()
        {
            Assert.Throw("TODO");
            //Assert.IsEqual(_selected.Count, 1);

            //var entry = _selected.Single();

            //switch (ClassifyList(entry.ListOwner))
            //{
                //case ListTypes.Package:
                //{
                    //AddBackgroundTask(OpenMoreInfoPopup((PackageInfo)entry.Tag));
                    //break;
                //}
                //case ListTypes.Release:
                //{
                    //AddBackgroundTask(OpenMoreInfoPopup((ReleaseInfo)entry.Tag));
                    //break;
                //}
                //default:
                //{
                    //Assert.Throw();
                    //break;
                //}
            //}
        }

        IEnumerator OpenMoreInfoPopup(PackageInfo info)
        {
            Assert.Throw("TODO");
            yield break;
        }

        void OpenReleaseFolderForSelected()
        {
            Assert.Throw("TODO");
            //Assert.IsEqual(_selected.Count, 1);

            //var info = (ReleaseInfo)_selected.Single().Tag;

            //Assert.IsNotNull(info.LocalPath);
            //PathUtil.AssertPathIsValid(info.LocalPath);

            //var args = @"/select, " + info.LocalPath;
            //System.Diagnostics.Process.Start("explorer.exe", args);
        }

        bool HasPackageYaml(PackageInfo info)
        {
            var configPath = Path.Combine(info.Path, ProjenyEditorUtil.PackageConfigFileName);
            return File.Exists(configPath);
        }

        void EditPackageYamlSelected()
        {
            Assert.Throw("TODO");
            //Assert.IsEqual(_selected.Count, 1);

            //var info = (PackageInfo)_selected.Single().Tag;

            //var configPath = Path.Combine(info.Path, ProjenyEditorUtil.PackageConfigFileName);

            //Assert.That(File.Exists(configPath));

            //InternalEditorUtility.OpenFileAtLineExternal(configPath, 1);
        }

        void RenameSelectedPackage()
        {
            Assert.Throw("TODO");
            //Assert.IsEqual(_selected.Count, 1);

            //var info = (PackageInfo)_selected.Single().Tag;

            //AddBackgroundTask(RenamePackageAsync(info));
        }

        IEnumerator RenamePackageAsync(PackageInfo info)
        {
            var newPackageName = _view.PromptForInput("Enter package name:", info.Name);

            yield return newPackageName;

            if (newPackageName.Current == null)
            {
                // User Cancelled
                yield break;
            }

            if (newPackageName.Current == info.Name)
            {
                yield break;
            }

            var dirInfo = new DirectoryInfo(info.Path);
            Assert.That(dirInfo.Name == info.Name);

            var newPath = Path.Combine(dirInfo.Parent.FullName, newPackageName.Current);

            Assert.That(!Directory.Exists(newPath), "Package with name '{0}' already exists.  Rename aborted.", newPackageName.Current);

            dirInfo.MoveTo(newPath);

            yield return RefreshPackagesAsync();

            Assert.Throw("TODO");
            //SelectInternal(_packagesList.Values
                //.Where(x => x.Name == newPackageName.Current).Single());
        }

        void OpenPackageFolderForSelected()
        {
            Assert.Throw("TODO");
            //Assert.IsEqual(_selected.Count, 1);

            //var info = (PackageInfo)_selected.Single().Tag;

            //Assert.That(Directory.Exists(info.Path));

            //System.Diagnostics.Process.Start(info.Path);
        }

        void SelectAll()
        {
            Assert.Throw("TODO");
            //if (_selected.IsEmpty())
            //{
                //return;
            //}

            //var listType = ClassifyList(_selected[0].ListOwner);

            //Assert.That(_selected.All(x => ClassifyList(x.ListOwner) == listType));

            //foreach (var entry in _selected[0].ListOwner.Values)
            //{
                //SelectInternal(entry);
            //}
        }

        void DeleteSelected()
        {
            AddBackgroundTask(DeleteSelectedAsync(), "Deleting Packages");
        }

        IEnumerator DeleteSelectedAsync()
        {
            Assert.Throw("TODO");
            yield break;
            //if (_selected.IsEmpty())
            //{
                //yield break;
            //}

            //var listType = ClassifyList(_selected[0].ListOwner);

            //Assert.That(_selected.All(x => ClassifyList(x.ListOwner) == listType));

            //if (listType == ListTypes.Package)
            //{
                //var infos = _selected.Select(x => (PackageInfo)x.Tag).ToList();

                //var choice = _view.PromptForUserChoice(
                    //"<color=yellow>Are you sure you wish to delete the following packages?</color>\n\n{0}\n\n<color=yellow>Please note the following:</color>\n\n- This change is not undoable\n- Any changes that you've made since installing will be lost\n- Any projects or other packages that still depend on this package may be put in an invalid state by deleting it".Fmt(infos.Select(x => "- " + x.Name).Join("\n")),
                    //new[] { "Delete", "Cancel" }, null, "DeleteSelectedPopupTextStyle");

                //yield return choice;

                //if (choice.Current == 0)
                //{
                    //yield return ProcessUpmCommand("Deleting selected packages", UpmHelper.DeletePackagesAsync(infos));
                    //yield return RefreshPackagesAsync();
                //}
            //}
            //else if (listType == ListTypes.AssetItem || listType == ListTypes.PluginItem)
            //{
                //var entriesToRemove = _selected.ToList();
                //_selected.Clear();

                //foreach (var entry in entriesToRemove)
                //{
                    //entry.ListOwner.Remove(entry);
                //}
            //}
        }

        IEnumerator InstallReleasesAsync(List<ReleaseInfo> releaseInfos)
        {
            // Need to make sure we have the most recent package list so we can determine whether this is
            // an upgrade / downgrade / etc.
            yield return RefreshPackagesAsync();

            Assert.That(releaseInfos.Select(x => x.Id).GetDuplicates().IsEmpty(), "Found duplicate releases selected - are you installing multiple versions of the same release?");

            foreach (var releaseInfo in releaseInfos)
            {
                var userChoice = CheckShouldInstall(releaseInfo);

                yield return userChoice;

                switch (userChoice.Current)
                {
                    case InstallReleaseUserChoices.Cancel:
                    {
                        yield break;
                    }
                    case InstallReleaseUserChoices.Install:
                    {
                        yield return ProcessUpmCommand("Installing release '{0}'".Fmt(releaseInfo.Name), UpmHelper.InstallReleaseAsync(releaseInfo));
                        break;
                    }
                    case InstallReleaseUserChoices.Skip:
                    {
                        // Do nothing
                        break;
                    }
                    default:
                    {
                        Assert.Throw();
                        break;
                    }
                }
            }

            yield return RefreshPackagesAsync();
        }

        IEnumerator<InstallReleaseUserChoices> CheckShouldInstall(ReleaseInfo releaseInfo)
        {
            return CoRoutine.Wrap<InstallReleaseUserChoices>(CheckShouldInstallInternal(releaseInfo));
        }

        IEnumerator CheckShouldInstallInternal(ReleaseInfo releaseInfo)
        {
            var packageInfo = TryFindPackageInfoForRelease(releaseInfo);

            if (packageInfo == null)
            {
                yield return InstallReleaseUserChoices.Install;
                yield break;
            }

            Assert.IsNotNull(packageInfo.InstallInfo);
            var packageReleaseInfo = packageInfo.InstallInfo.ReleaseInfo;

            Assert.IsNotNull(packageReleaseInfo);

            // TODO - how to handle?
            Assert.That(packageReleaseInfo.HasVersionCode);
            Assert.That(releaseInfo.HasVersionCode);

            IEnumerator<int> userChoice;

            if (packageReleaseInfo.VersionCode == releaseInfo.VersionCode)
            {
                Assert.IsEqual(releaseInfo.Version, packageReleaseInfo.Version);

                userChoice = _view.PromptForUserChoice(
                    "Package '{0}' is already installed with the same version ('{1}').  Would you like to re-install it anyway?  Note that any local changes you've made to the package will be reverted."
                        .Fmt(packageReleaseInfo.Name, packageReleaseInfo.Version), new[] { "Overwrite", "Skip", "Cancel" });
            }
            else if (releaseInfo.VersionCode > packageReleaseInfo.VersionCode)
            {
                userChoice = _view.PromptForUserChoice(
                    "Package '{0}' is already installed with version '{1}'. Would you like to UPGRADE it to version '{2}'?  Note that any local changes you've made to the package will be lost."
                    .Fmt(releaseInfo.Name, packageReleaseInfo.Version, releaseInfo.Version), new[] { "Upgrade", "Skip", "Cancel" });
            }
            else
            {
                Assert.That(releaseInfo.VersionCode < packageReleaseInfo.VersionCode);

                userChoice = _view.PromptForUserChoice(
                    "Package '{0}' is already installed with version '{1}'. Would you like to DOWNGRADE it to version '{2}'?  Note that any local changes you've made to the package will be lost."
                    .Fmt(releaseInfo.Name, packageReleaseInfo.Version, releaseInfo.Version), new[] { "Downgrade", "Skip", "Cancel" });
            }

            yield return userChoice;

            switch (userChoice.Current)
            {
                case 0:
                {
                    yield return InstallReleaseUserChoices.Install;
                    break;
                }
                case 1:
                {
                    yield return InstallReleaseUserChoices.Skip;
                    break;
                }
                case 2:
                {
                    yield return InstallReleaseUserChoices.Cancel;
                    break;
                }
                default:
                {
                    Assert.Throw();
                    break;
                }
            }
        }

        PackageInfo TryFindPackageInfoForRelease(ReleaseInfo releaseInfo)
        {
            foreach (var packageInfo in _model.Packages)
            {
                if (packageInfo.InstallInfo != null && packageInfo.InstallInfo.ReleaseInfo != null && packageInfo.InstallInfo.ReleaseInfo.Id == releaseInfo.Id)
                {
                    return packageInfo;
                }
            }

            return null;
        }

        ListTypes ClassifyList(DraggableList list)
        {
            Assert.Throw("TODO");
            return ListTypes.AssetItem;
        }

        IEnumerator RefreshReleasesAsync()
        {
            var response = ProcessUpmCommandForResult<List<ReleaseInfo>>("Looking up release list", UpmHelper.LookupReleaseListAsync());
            yield return response;

            _model.SetReleases(response.Current);
            UpdateAvailableReleasesList();
        }

        IEnumerator<T> ProcessUpmCommandForResult<T>(string statusName, IEnumerator upmTask)
        {
            return CoRoutine.Wrap<T>(ProcessUpmCommand(statusName, upmTask));
        }

        IEnumerator ProcessUpmCommand(string statusName, IEnumerator upmTask)
        {
            Assert.IsNull(_view.BlockedStatusMessage);
            _view.BlockedStatusMessage = statusName;

            while (upmTask.MoveNext())
            {
                if (upmTask.Current is UpmHelperResponse)
                {
                    Assert.That(!upmTask.MoveNext());
                    break;
                }

                if (upmTask.Current != null)
                {
                    Assert.IsType<List<string>>(upmTask.Current);
                    var outputLines = (List<string>)upmTask.Current;

                    if (outputLines.Count > 0)
                    {
                        _view.BlockedStatusMessage = outputLines.Last();
                    }
                }

                yield return null;
            }

            Assert.IsType<UpmHelperResponse>(upmTask.Current);
            var response = (UpmHelperResponse)upmTask.Current;

            // Refresh assets regardless of what kind of UpmCommand this was
            // This is good because many commands can affect the project
            // Including installing a package, deleting a package, etc.
            AssetDatabase.Refresh();

            _view.BlockedStatusMessage = null;

            if (response.Succeeded)
            {
                yield return response.Result;
            }
            else
            {
                throw new UpmCommandException(response.ErrorMessage);
            }
        }

        void UpdateAvailableReleasesList()
        {
            Assert.Throw("TODO");
            //_releasesList.Clear();

            //foreach (var info in _allReleases)
            //{
                //_releasesList.Add(info.Name, info);
            //}
        }

        void ForceStopBackgroundTask()
        {
            Assert.Throw("TODO");
            //_backgroundTaskInfo = null;
        }

        void AddBackgroundTask(IEnumerator task, string title = null)
        {
            _asyncProcessor.Process(RunMainBackgroundTask(task, title));
        }

        IEnumerator RunMainBackgroundTask(IEnumerator task, string title)
        {
            Assert.IsNull(_view.BlockedStatusTitle);
            Assert.IsNull(_view.BlockedStatusMessage);

            _view.BlockedStatusTitle = title;

            yield return task;

            Assert.IsEqual(_view.BlockedStatusTitle, title);
            _view.BlockedStatusTitle = null;
        }

        IEnumerator CreateNewPackageAsync()
        {
            var userInput = _view.PromptForInput("Enter new package name:", "Untitled");

            yield return userInput;

            if (userInput.Current == null)
            {
                // User Cancelled
                yield break;
            }

            yield return ProcessUpmCommand("Creating Package '{0}'".Fmt(userInput.Current), UpmHelper.CreatePackageAsync(userInput.Current));
            yield return RefreshPackagesAsync();
        }

        IEnumerator RefreshPackagesAsync()
        {
            var allPackages = ProcessUpmCommandForResult<List<PackageInfo>>("Looking up package list", UpmHelper.LookupPackagesListAsync());
            yield return allPackages;

            _model.SetPackages(allPackages.Current);
        }

        void RefreshProject()
        {
            var configPath = ProjenyEditorUtil.GetProjectConfigPath(_model.ProjectConfigType);

            if (!File.Exists(configPath))
            {
                ResetProject();
                return;
            }

            ProjectConfig savedConfig;

            try
            {
                savedConfig = DeserializeProjectConfig(configPath);
            }
            catch (Exception e)
            // This can happen if the file has yaml serialization errors
            {
                DisplayError(e.Message);
                return;
            }

            // Null when file is empty
            if (savedConfig == null)
            {
                ResetProject();
            }
            else
            {
                PopulateModelFromConfig(savedConfig);
            }
        }

        void DisplayError(string message)
        {
            Log.Error("Projeny: " + message);

            // Do not display errors on top of each other
            // In those cases it will still be in the log and that's enough
            if (!_isDisplayingError)
            {
                _asyncProcessor.Process(DisplayErrorInternal(message));
            }
        }

        IEnumerator DisplayErrorInternal(string message)
        {
            Assert.That(!_isDisplayingError);
            _isDisplayingError = true;

            yield return _view.AlertUser(message, "<color=red>Error!</color>");

            _isDisplayingError = false;
        }

        void ResetProject()
        {
            _model.ClearAssetItems();
            _model.ClearPluginItems();
        }

        void PopulateModelFromConfig(ProjectConfig config)
        {
            _model.ClearPluginItems();

            foreach (var name in config.PackagesPlugins)
            {
                _model.AddPluginItem(name);
            }

            _model.ClearAssetItems();

            foreach (var name in config.Packages)
            {
                _model.AddAssetItem(name);
            }
        }

        void OverwriteConfig()
        {
            File.WriteAllText(ProjenyEditorUtil.GetProjectConfigPath(_model.ProjectConfigType), GetSerializedProjectConfigFromLists());
        }

        bool HasProjectConfigChanged()
        {
            var configPath = ProjenyEditorUtil.GetProjectConfigPath(_model.ProjectConfigType);

            var currentConfig = GetProjectConfigFromLists();

            if (!File.Exists(configPath))
            {
                return !currentConfig.Packages.IsEmpty() || !currentConfig.PackagesPlugins.IsEmpty();
            }

            ProjectConfig savedConfig;

            try
            {
                savedConfig = DeserializeProjectConfig(configPath);
            }
            catch (Exception e)
            {
                Log.ErrorException(e);
                // This happens if we have serialization errors
                // Just log the error then assume that the file is different in this case so that the user
                // has the option to overwrite
                return true;
            }

            if (savedConfig == null)
            {
                return !currentConfig.Packages.IsEmpty() || !currentConfig.PackagesPlugins.IsEmpty();
            }

            return !Enumerable.SequenceEqual(currentConfig.Packages.OrderBy(t => t), savedConfig.Packages.OrderBy(t => t))
                || !Enumerable.SequenceEqual(currentConfig.PackagesPlugins.OrderBy(t => t), savedConfig.PackagesPlugins.OrderBy(t => t));
        }

        ProjectConfig DeserializeProjectConfig(string configPath)
        {
            try
            {
                return UpmSerializer.DeserializeProjectConfig(File.ReadAllText(configPath));
            }
            catch (Exception e)
            {
                throw new Exception(
                    "Error while reading from '{0}': \n\n{1}".Fmt(Path.GetFileName(configPath), e.Message));
            }
        }

        ProjectConfig GetProjectConfigFromLists()
        {
            var config = new ProjectConfig();

            config.Packages = _model.AssetItems.ToList();
            config.PackagesPlugins = _model.PluginItems.ToList();

            return config;
        }

        string GetSerializedProjectConfigFromLists()
        {
            return UpmSerializer.SerializeProjectConfig(GetProjectConfigFromLists());
        }

        IEnumerator TryChangeProjectType(ProjectConfigTypes configType)
        {
            if (HasProjectConfigChanged())
            {
                var choice = _view.PromptForUserChoice(
                    "Do you want to save changes to your project?", new[] { "Save", "Don't Save", "Cancel" });

                yield return choice;

                switch (choice.Current)
                {
                    case 0:
                    {
                        OverwriteConfig();
                        break;
                    }
                    case 1:
                    {
                        // Do nothing
                        break;
                    }
                    case 2:
                    {
                        yield break;
                    }
                    default:
                    {
                        Assert.Throw();
                        break;
                    }
                }
            }

            Assert.Throw("TODO");
            //_projectConfigType = configType;
            //RefreshProject();
        }

        public void OnGUI()
        {
            var fullRect = new Rect(0, 0, this.position.width, this.position.height);
            _view.OnGUI(fullRect);

            CheckForKeypresses();
        }

        void CheckForKeypresses()
        {
            if (!GUI.enabled)
            {
                // Popup visible
                return;
            }

            var e = Event.current;

            if (e.type == EventType.ValidateCommand)
            {
                if (e.commandName == "SelectAll")
                {
                    e.Use();
                }
            }
            else if (e.type == EventType.ExecuteCommand)
            {
                if (e.commandName == "SelectAll")
                {
                    SelectAll();
                    e.Use();
                }
            }
            else if (e.type == EventType.KeyDown)
            {
                switch (e.keyCode)
                {
                    case KeyCode.Delete:
                    {
                        DeleteSelected();
                        e.Use();
                        break;
                    }
                }
            }
        }

        public class UpmCommandException : Exception
        {
            public UpmCommandException(string message)
                : base(message)
            {
            }
        }

        public class BackgroundTaskInfo
        {
            public CoRoutine CoRoutine;

            public string ProcessName;
            public string StatusTitle;
            public string StatusMessage;
        }

        enum InstallReleaseUserChoices
        {
            Install,
            Cancel,
            Skip,
        }
    }
}

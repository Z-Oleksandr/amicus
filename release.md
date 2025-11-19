# Prepare for release

## Section 1

The app now needs to be prepared for realse. This means adding the following:

-   The app needs an installer
-   User should be able to download an installer -> run it -> and then have the app be installed on the computer
-   The app should be added to the apps that run on computer start up and also added to the desktop

## After the installation the first start up:

-   After the app has been installed and is performing the first, fresh start -> it should go into initial setup mode, where:
    -   a dialog window is displayed in the middle of the screen
    -   the dialog window is styled in the cozy/soft colors, like the rest of the app
    -   The user names their cat, (this should be saved in persistance)
    -   The user picks colors for the items in the pet house (this should also be saved in persistance and then on each start up correct decorations loaded)
        -   For this a demo of the house should be displayed, where each element could be clicked to cycle through colors
    -   A save button down below
    -   After save button pressed -> go into normal mode
    -   Next start up uses already saved data

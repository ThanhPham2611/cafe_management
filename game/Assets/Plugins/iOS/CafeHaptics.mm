// Rung iOS cho game (Haptics.cs gọi qua P/Invoke). kind: 1 Light, 2 Medium, 3 Success.
#import <UIKit/UIKit.h>

static UIImpactFeedbackGenerator *gLight = nil;
static UIImpactFeedbackGenerator *gMedium = nil;
static UINotificationFeedbackGenerator *gNote = nil;

extern "C" void _CafeHaptic(int kind)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (kind == 1)
        {
            if (gLight == nil) gLight = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
            [gLight impactOccurred];
            [gLight prepare];
        }
        else if (kind == 2)
        {
            if (gMedium == nil) gMedium = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium];
            [gMedium impactOccurred];
            [gMedium prepare];
        }
        else if (kind == 3)
        {
            if (gNote == nil) gNote = [[UINotificationFeedbackGenerator alloc] init];
            [gNote notificationOccurred:UINotificationFeedbackTypeSuccess];
            [gNote prepare];
        }
    });
}

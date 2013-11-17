This patch works for PC_Navigator and up.

This patch expects Navigator Plugin for Centrafuse V3 to be installed.

To install this patch, copy the files in this folder to the PC_Navigator run time folder and
execute the patch_CF_NP batch file.


This patch will modify the files PC_navigatore so these modules referrence to the 
adjusted DLL CF_NP in stead of the Windows DLL WINMM.

This modified DLL will send a Mute command to the Navigator Plugin for Centrafuse through a 
named pipe interface whenever Navigator starts some kind of audio. When Navigator is finished with this 
audio, it will send an UnMute command through this named pipe.


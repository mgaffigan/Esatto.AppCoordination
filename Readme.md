# Esatto Application Coordination

Allow multiple applications to communicate with each other within the same session.  A 
"shell" application exposes the fact that an entity is open, and other applications can
add commands or otherwise change their behavior based on the shell application.

A background process called the coordianator receives notices from each coordinated 
application and distributes them to all connected clients.
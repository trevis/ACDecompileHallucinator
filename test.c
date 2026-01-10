

/* 1707 */
struct __declspec(align(4)) IInputActionCallback
{
  IInputActionCallback_vtbl *__vftable /*VFT*/;
};

/* 2725 */
struct /*VFT*/ __declspec(align(4)) IInputActionCallback_vtbl
{
  void (__thiscall *IInputActionCallback_dtor_0)(IInputActionCallback *this);
  CallbackLoseFocusResult (__thiscall *OnLoseFocus)(IInputActionCallback *this, const unsigned int, unsigned int, unsigned int);
};



/* 1711 */
struct __declspec(align(8)) ACCmdInterp
{
  CommandInterpreter baseclass_0;
  gmNoticeHandler baseclass_c8;
  HashTable<unsigned long,unsigned long,0> m_hashEmoteInputActionsToCommands;
};

/* 1712 */
struct /*VFT*/ __declspec(align(4)) ACCmdInterp_vtbl
{
  void (__thiscall *ACCmdInterp_dtor_0)(struct ACCmdInterp *this);
};
